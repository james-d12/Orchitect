using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Orchitect.Api.Authentication;
using Orchitect.Api.Endpoints.Internal;
using Orchitect.Domain.Engine.Deployment;
using Orchitect.Engine.Contracts.Runner.Api;
using Orchitect.Engine.Dispatch.Auth;

namespace Orchitect.Api.Integration.Tests;

public sealed class RunnerAuthenticationTests : IAsyncLifetime
{
    private const string Issuer = "orchitect-runner-auth-tests";
    private const string Audience = "orchitect-runner-auth-tests";
    private const string Secret = "runner-auth-test-jwt-secret-key-orchitect-platform";
    private const string UserUrl = "/user";

    private readonly InMemoryDeploymentRunRepository _runs = new();
    private WebApplication _app = null!;
    private HttpClient _client = null!;

    public async Task InitializeAsync()
    {
        var builder = WebApplication.CreateEmptyBuilder(new WebApplicationOptions());
        builder.WebHost.UseTestServer();
        builder.Services.AddRouting();
        builder.Services.AddLogging();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["JwtOptions:Issuer"] = Issuer,
            ["JwtOptions:Audience"] = Audience,
            ["JwtOptions:ExpirationInMinutes"] = "5",
            ["JwtOptions:Secret"] = Secret
        });
        builder.Services.AddOrchitectAuthentication(builder.Configuration);
        builder.Services.AddSingleton<IDeploymentRunRepository>(_runs);

        _app = builder.Build();
        _app.UseAuthentication();
        _app.UseAuthorization();
        _app.MapRunnerGroup()
            .MapGet("/", (ClaimsPrincipal user) => user.FindFirstValue(RunnerAuthenticationDefaults.RunIdClaim));
        _app.MapGroup(UserUrl).RequireAuthorization().MapGet("/", () => "ok");

        await _app.StartAsync();
        _client = _app.GetTestClient();
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _app.DisposeAsync();
    }

    [Fact]
    public async Task RunnerApi_WhenTokenIsValidForRouteRun_ShouldReturn200Ok()
    {
        // Arrange
        var (run, token) = IssueToken(Running());

        // Act
        var response = await SendAsync(RunUrl(run.Id), token.Value);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(run.Id.Value.ToString(), await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task RunnerApi_WhenRunIsQueued_ShouldReturn200Ok()
    {
        // Arrange
        var (run, token) = IssueToken(Queued());

        // Act
        var response = await SendAsync(RunUrl(run.Id), token.Value);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task RunnerApi_WhenNoToken_ShouldReturn401Unauthorized()
    {
        // Arrange
        var (run, _) = IssueToken(Running());

        // Act
        var response = await SendAsync(RunUrl(run.Id), null);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task RunnerApi_WhenTokenIsUnknown_ShouldReturn401Unauthorized()
    {
        // Arrange
        var (run, _) = IssueToken(Running());

        // Act
        var response = await SendAsync(RunUrl(run.Id), RunnerToken.Generate().Value);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task RunnerApi_WhenTokenHasExpired_ShouldReturn401Unauthorized()
    {
        // Arrange
        var (run, token) = IssueToken(Running(), DateTime.UtcNow.AddSeconds(-1));

        // Act
        var response = await SendAsync(RunUrl(run.Id), token.Value);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public async Task RunnerApi_WhenTokenWasRevokedByCompletion_ShouldReturn401Unauthorized(long exitCode)
    {
        // Arrange
        var (run, token) = IssueToken(Running());
        _runs.Update(run.Complete(exitCode, null));

        // Act
        var response = await SendAsync(RunUrl(run.Id), token.Value);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task RunnerApi_WhenTokenWasRevokedByInterruption_ShouldReturn401Unauthorized()
    {
        // Arrange
        var (run, token) = IssueToken(Running());
        _runs.Update(run.Interrupt("The API restarted before the run finished."));

        // Act
        var response = await SendAsync(RunUrl(run.Id), token.Value);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData(DeploymentRunStatus.Succeeded)]
    [InlineData(DeploymentRunStatus.Failed)]
    [InlineData(DeploymentRunStatus.Cancelled)]
    public async Task RunnerApi_WhenRunIsNotQueuedOrRunning_ShouldReturn401Unauthorized(DeploymentRunStatus status)
    {
        // Arrange
        var (run, token) = IssueToken(Running());
        _runs.Update(run with { Status = status });

        // Act
        var response = await SendAsync(RunUrl(run.Id), token.Value);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task RunnerApi_WhenTokenIsForAnotherRun_ShouldReturn401Unauthorized()
    {
        // Arrange
        var (_, token) = IssueToken(Running());
        var (other, _) = IssueToken(Running());

        // Act
        var response = await SendAsync(RunUrl(other.Id), token.Value);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task RunnerApi_WhenUserJwtIsUsed_ShouldReturn401Unauthorized()
    {
        // Arrange
        var (run, _) = IssueToken(Running());

        // Act
        var response = await SendAsync(RunUrl(run.Id), CreateUserJwt());

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task UserApi_WhenRunnerTokenIsUsed_ShouldReturn401Unauthorized()
    {
        // Arrange
        var (_, token) = IssueToken(Running());

        // Act
        var response = await SendAsync(UserUrl, token.Value);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task UserApi_WhenUserJwtIsUsed_ShouldReturn200Ok()
    {
        // Act
        var response = await SendAsync(UserUrl, CreateUserJwt());

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private static string RunUrl(DeploymentRunId runId) => $"/internal/runs/{runId.Value}";

    private static DeploymentRun Queued() => DeploymentRun.Queue(new DeploymentId(), DeploymentRunOperation.Provision);

    private static DeploymentRun Running() => Queued().Start();

    private (DeploymentRun, RunnerToken) IssueToken(DeploymentRun run, DateTime? expiresAt = null)
    {
        var token = RunnerToken.Generate();
        var issued = run.IssueToken(token.Hash, expiresAt ?? DateTime.UtcNow.AddHours(1));
        _runs.Update(issued);
        return (issued, token);
    }

    private async Task<HttpResponseMessage> SendAsync(string url, string? bearerToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Add(RunnerContract.HeaderName, RunnerContract.Version.ToString());

        if (bearerToken is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);
        }

        return await _client.SendAsync(request);
    }

    private static string CreateUserJwt() => new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
    {
        Subject = new ClaimsIdentity([new Claim(JwtRegisteredClaimNames.Sub, Guid.NewGuid().ToString())]),
        Issuer = Issuer,
        Audience = Audience,
        Expires = DateTime.UtcNow.AddMinutes(5),
        SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Secret)),
            SecurityAlgorithms.HmacSha256)
    });

    private sealed class InMemoryDeploymentRunRepository : IDeploymentRunRepository
    {
        private readonly Dictionary<DeploymentRunId, DeploymentRun> _runs = [];

        public void Update(DeploymentRun run) => _runs[run.Id] = run;

        public Task<DeploymentRun?> GetByTokenHashAsync(string tokenHash,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(_runs.Values.SingleOrDefault(r => r.TokenHash == tokenHash));

        public Task<DeploymentRun?> GetByIdAsync(DeploymentRunId id, CancellationToken cancellationToken = default) =>
            Task.FromResult(_runs.GetValueOrDefault(id));

        public Task<DeploymentRun?> UpdateAsync(DeploymentRun run, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<DeploymentRun?> CreateAsync(DeploymentRun run, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public IEnumerable<DeploymentRun> GetAll() => throw new NotSupportedException();

        public Task<DeploymentRun?> GetLatestAsync(DeploymentId deploymentId,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
