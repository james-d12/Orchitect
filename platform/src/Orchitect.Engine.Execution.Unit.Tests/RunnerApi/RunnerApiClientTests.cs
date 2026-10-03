using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Orchitect.Engine.Contracts.Runner;
using Orchitect.Engine.Contracts.Runner.Api;
using Orchitect.Engine.Execution.RunnerApi;

namespace Orchitect.Engine.Execution.Unit.Tests.RunnerApi;

public sealed class RunnerApiClientTests
{
    private const string Token = "run-token";
    private const int MaxRetryAttempts = 2;
    private static readonly Guid RunId = Guid.NewGuid();

    [Fact]
    public async Task GetRunAsync_SendsTokenAndContractVersionToTheRunRoute()
    {
        var descriptor = new RunDescriptor(RunId, RunnerOperation.Destroy, new Uri("https://github.com/test/repo"),
            new string('a', 40), Guid.NewGuid(), Guid.NewGuid());
        var handler = new StubHandler(_ => JsonResponse(descriptor));

        var result = await CreateClient(handler).GetRunAsync(CancellationToken.None);

        Assert.Equal(descriptor, result);
        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Equal($"http://orchitect-api:8080/base/internal/runs/{RunId}", request.RequestUri?.ToString());
        Assert.Equal("Bearer", request.Headers.Authorization?.Scheme);
        Assert.Equal(Token, request.Headers.Authorization?.Parameter);
        Assert.Equal(RunnerContract.Version.ToString(),
            Assert.Single(request.Headers.GetValues(RunnerContract.HeaderName)));
    }

    [Theory]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    [InlineData(HttpStatusCode.BadGateway)]
    [InlineData(HttpStatusCode.RequestTimeout)]
    [InlineData(HttpStatusCode.TooManyRequests)]
    public async Task GetRunAsync_TransientFailure_RetriesUntilItSucceeds(HttpStatusCode status)
    {
        var descriptor = new RunDescriptor(RunId, RunnerOperation.Provision, new Uri("https://github.com/test/repo"),
            new string('b', 40), Guid.NewGuid(), Guid.NewGuid());
        var handler = new StubHandler(attempt =>
            attempt < MaxRetryAttempts ? new HttpResponseMessage(status) : JsonResponse(descriptor));

        var result = await CreateClient(handler).GetRunAsync(CancellationToken.None);

        Assert.Equal(descriptor, result);
        Assert.Equal(MaxRetryAttempts + 1, handler.Requests.Count);
    }

    [Fact]
    public async Task GetRunAsync_NetworkFailure_Retries()
    {
        var handler = new StubHandler(attempt => attempt == 0
            ? throw new HttpRequestException("Connection refused.")
            : JsonResponse(new RunDescriptor(RunId, RunnerOperation.Provision,
                new Uri("https://github.com/test/repo"), new string('c', 40), Guid.NewGuid(), Guid.NewGuid())));

        await CreateClient(handler).GetRunAsync(CancellationToken.None);

        Assert.Equal(2, handler.Requests.Count);
    }

    [Fact]
    public async Task GetRunAsync_TransientFailurePersists_ThrowsAfterTheLastRetry()
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));

        var exception = await Assert.ThrowsAsync<HttpRequestException>(() =>
            CreateClient(handler).GetRunAsync(CancellationToken.None));

        Assert.Equal(HttpStatusCode.ServiceUnavailable, exception.StatusCode);
        Assert.Equal(MaxRetryAttempts + 1, handler.Requests.Count);
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest)]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.NotFound)]
    public async Task GetRunAsync_NonTransientFailure_DoesNotRetry(HttpStatusCode status)
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(status));

        var exception = await Assert.ThrowsAsync<HttpRequestException>(() =>
            CreateClient(handler).GetRunAsync(CancellationToken.None));

        Assert.Equal(status, exception.StatusCode);
        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task GetRunAsync_WithDefaultResilienceHandler_RetriesOnlyThroughTheRunnerPipeline()
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));

        await Assert.ThrowsAsync<HttpRequestException>(() =>
            CreateClient(handler, services =>
                    services.ConfigureHttpClientDefaults(http => http.AddStandardResilienceHandler()))
                .GetRunAsync(CancellationToken.None));

        Assert.Equal(MaxRetryAttempts + 1, handler.Requests.Count);
    }

    [Fact]
    public async Task SubmitScoreAsync_PostsTheScoreAndReturnsThePlan()
    {
        var plan = new RunPlan(new RunContext("project", Guid.NewGuid(), Guid.NewGuid()),
        [
            new RunInput("db", "postgres", new RunInputSource(new Uri("https://github.com/test/modules"), "v1", null),
                new Dictionary<string, string> { ["size"] = "small" })
        ]);
        var handler = new StubHandler(_ => JsonResponse(plan));

        var result = await CreateClient(handler).SubmitScoreAsync(
            new ScoreSubmission(new Contracts.Score.ScoreFile
            {
                ApiVersion = "score.dev/v1b1",
                Metadata = new Contracts.Score.ScoreMetadata { Name = "project" }
            }), CancellationToken.None);

        Assert.Equal(plan.Context, result.Context);
        Assert.Equal("db", Assert.Single(result.Inputs).Key);
        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.EndsWith($"/internal/runs/{RunId}{RunnerRoutes.Plan}", request.RequestUri?.ToString());
    }

    [Fact]
    public async Task CompleteAsync_TransientFailure_RetriesWithTheSameBody()
    {
        var handler = new StubHandler(attempt => attempt == 0
            ? new HttpResponseMessage(HttpStatusCode.BadGateway)
            : new HttpResponseMessage(HttpStatusCode.NoContent));

        await CreateClient(handler).CompleteAsync(new RunCompletion(RunOutcome.Failed, "terraform apply failed"),
            CancellationToken.None);

        Assert.Equal(2, handler.Requests.Count);
        Assert.All(handler.Bodies, body => Assert.Equal(
            """{"outcome":"Failed","errorSummary":"terraform apply failed"}""", body));
        Assert.EndsWith($"/internal/runs/{RunId}{RunnerRoutes.Complete}",
            handler.Requests[^1].RequestUri?.ToString());
    }

    [Fact]
    public async Task GetRunAsync_AttemptHangs_TimesOutAndRetries()
    {
        var descriptor = new RunDescriptor(RunId, RunnerOperation.Provision, new Uri("https://github.com/test/repo"),
            new string('d', 40), Guid.NewGuid(), Guid.NewGuid());
        var handler = new StubHandler(async (attempt, cancellationToken) =>
        {
            if (attempt == 0)
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            }

            return JsonResponse(descriptor);
        });

        var result = await CreateClient(handler).GetRunAsync(CancellationToken.None);

        Assert.Equal(descriptor, result);
        Assert.Equal(2, handler.Requests.Count);
    }

    [Theory]
    [InlineData(null, "8b6d6c0e-3f0a-4ad5-9a40-6f3b1f6f4a61", Token, RunnerEnvironment.ApiBaseUrl)]
    [InlineData("not-a-url", "8b6d6c0e-3f0a-4ad5-9a40-6f3b1f6f4a61", Token, RunnerEnvironment.ApiBaseUrl)]
    [InlineData("http://orchitect-api:8080", null, Token, RunnerEnvironment.RunId)]
    [InlineData("http://orchitect-api:8080", "not-a-guid", Token, RunnerEnvironment.RunId)]
    [InlineData("http://orchitect-api:8080", "8b6d6c0e-3f0a-4ad5-9a40-6f3b1f6f4a61", null, RunnerEnvironment.RunToken)]
    public void AddRunnerApiClient_InvalidSetting_FailsValidationNamingTheSetting(string? baseUrl, string? runId,
        string? token, string expectedSetting)
    {
        using var provider = BuildProvider(new StubHandler(_ => new HttpResponseMessage()), baseUrl, runId, token);

        var exception =
            Assert.Throws<OptionsValidationException>(() => provider.GetRequiredService<IRunnerApiClient>());

        Assert.StartsWith(expectedSetting, Assert.Single(exception.Failures));
    }

    private static IRunnerApiClient CreateClient(StubHandler handler, Action<IServiceCollection>? configure = null)
    {
        var provider = BuildProvider(handler, "http://orchitect-api:8080/base", RunId.ToString(), Token, configure);
        return provider.GetRequiredService<IRunnerApiClient>();
    }

    private static ServiceProvider BuildProvider(StubHandler handler, string? baseUrl, string? runId, string? token,
        Action<IServiceCollection>? configure = null)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [RunnerEnvironment.ApiBaseUrl] = baseUrl,
                [RunnerEnvironment.RunId] = runId,
                [RunnerEnvironment.RunToken] = token
            })
            .Build();

        var services = new ServiceCollection();
        configure?.Invoke(services);
        services.AddRunnerApiClient(configuration);
        services.PostConfigure<RunnerApiOptions>(options =>
        {
            options.MaxRetryAttempts = MaxRetryAttempts;
            options.RetryDelay = TimeSpan.FromMilliseconds(1);
            options.AttemptTimeout = TimeSpan.FromMilliseconds(200);
        });
        services.AddHttpClient<IRunnerApiClient, RunnerApiClient>().ConfigurePrimaryHttpMessageHandler(() => handler);

        return services.BuildServiceProvider();
    }

    private static HttpResponseMessage JsonResponse<T>(T value) =>
        new(HttpStatusCode.OK) { Content = JsonContent.Create(value, options: RunnerContract.JsonOptions) };

    private sealed class StubHandler(Func<int, CancellationToken, Task<HttpResponseMessage>> respond)
        : HttpMessageHandler
    {
        public StubHandler(Func<int, HttpResponseMessage> respond)
            : this((attempt, _) => Task.FromResult(respond(attempt)))
        {
        }

        public List<HttpRequestMessage> Requests { get; } = [];
        public List<string> Bodies { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Requests.Add(request);

            if (request.Content is not null)
            {
                Bodies.Add(await request.Content.ReadAsStringAsync(cancellationToken));
            }

            return await respond(Requests.Count - 1, cancellationToken);
        }
    }
}
