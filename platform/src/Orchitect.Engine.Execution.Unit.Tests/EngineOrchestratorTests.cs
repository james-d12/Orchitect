using Microsoft.Extensions.Logging.Abstractions;
using Orchitect.Domain.Core.Organisation;
using Orchitect.Domain.Engine.Application;
using Orchitect.Domain.Engine.Deployment;
using Orchitect.Domain.Engine.Environment;
using Orchitect.Engine.Contracts.Runner.Api;
using Orchitect.Engine.Contracts.Score;
using Orchitect.Engine.Execution.Configuration.Score;
using Orchitect.Engine.Execution.Provisioner;
using Orchitect.Engine.Execution.RunnerApi;
using Orchitect.Engine.Execution.Unit.Tests.Terraform;
using ApplicationId = Orchitect.Domain.Engine.Application.ApplicationId;

namespace Orchitect.Engine.Execution.Unit.Tests;

public sealed class EngineOrchestratorTests
{
    private readonly RecordingProvisioner _provisioner = new();
    private readonly FakeRunnerApiClient _runnerApi = new();
    private readonly Application _application = NewApplication();
    private readonly Deployment _deployment = NewDeployment();

    [Fact]
    public async Task StartAsync_PlanReturned_SubmitsScoreProvisionsPlanAndReportsSuccess()
    {
        var scoreFile = Score();

        await CreateOrchestrator(scoreFile).StartAsync(_application, _deployment, CancellationToken.None);

        Assert.Same(scoreFile, _runnerApi.Submission?.ScoreFile);
        Assert.Equal(_runnerApi.Plan.Inputs, _provisioner.Provisioned);
        Assert.Same(_runnerApi.Plan.Context, _provisioner.Context);
        Assert.Null(_provisioner.Deleted);
        Assert.Equal(new RunCompletion(RunOutcome.Succeeded, null), Assert.Single(_runnerApi.Completions));
    }

    [Fact]
    public async Task DestroyAsync_PlanReturned_DeletesPlanAndReportsSuccess()
    {
        await CreateOrchestrator(Score()).DestroyAsync(_application, _deployment, CancellationToken.None);

        Assert.Equal(_runnerApi.Plan.Inputs, _provisioner.Deleted);
        Assert.Null(_provisioner.Provisioned);
        Assert.Equal(RunOutcome.Succeeded, Assert.Single(_runnerApi.Completions).Outcome);
    }

    [Fact]
    public async Task StartAsync_ProvisionFails_ReportsFailureAndRethrows()
    {
        _provisioner.Failure = new InvalidOperationException("terraform apply failed");

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CreateOrchestrator(Score()).StartAsync(_application, _deployment, CancellationToken.None));

        Assert.Same(_provisioner.Failure, exception);
        Assert.Equal(new RunCompletion(RunOutcome.Failed, "terraform apply failed"),
            Assert.Single(_runnerApi.Completions));
    }

    [Fact]
    public async Task DestroyAsync_DeleteFails_ReportsFailure()
    {
        _provisioner.Failure = new InvalidOperationException("terraform destroy failed");

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CreateOrchestrator(Score()).DestroyAsync(_application, _deployment, CancellationToken.None));

        Assert.Equal(RunOutcome.Failed, Assert.Single(_runnerApi.Completions).Outcome);
    }

    [Fact]
    public async Task StartAsync_ScoreFileMissing_ReportsFailureWithoutPlanning()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CreateOrchestrator(null).StartAsync(_application, _deployment, CancellationToken.None));

        Assert.Null(_runnerApi.Submission);
        Assert.Null(_provisioner.Provisioned);
        Assert.Equal(RunOutcome.Failed, Assert.Single(_runnerApi.Completions).Outcome);
    }

    [Fact]
    public async Task StartAsync_PlanRejected_ReportsFailureWithoutProvisioning()
    {
        _runnerApi.PlanFailure = new HttpRequestException("400 Bad Request");

        await Assert.ThrowsAsync<HttpRequestException>(() =>
            CreateOrchestrator(Score()).StartAsync(_application, _deployment, CancellationToken.None));

        Assert.Null(_provisioner.Provisioned);
        Assert.Equal(new RunCompletion(RunOutcome.Failed, "400 Bad Request"), Assert.Single(_runnerApi.Completions));
    }

    [Fact]
    public async Task StartAsync_FailureReportFails_RethrowsTheOriginalFailure()
    {
        _provisioner.Failure = new InvalidOperationException("terraform apply failed");
        _runnerApi.CompleteFailure = new HttpRequestException("API unavailable");

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CreateOrchestrator(Score()).StartAsync(_application, _deployment, CancellationToken.None));

        Assert.Same(_provisioner.Failure, exception);
    }

    private EngineOrchestrator CreateOrchestrator(ScoreFile? scoreFile) =>
        new(NullLogger<EngineOrchestrator>.Instance, new FixedScoreDriver(scoreFile), _runnerApi, _provisioner);

    private static ScoreFile Score() => new()
    {
        ApiVersion = "score.dev/v1b1",
        Metadata = new ScoreMetadata { Name = "orders" },
        Resources = new Dictionary<string, ScoreResource>
        {
            ["storage"] = new() { Type = "azure-storage-account" }
        }
    };

    private static Application NewApplication() =>
        Application.Create("orders", new Repository
        {
            Name = "orders",
            Url = new Uri("https://example.com/orders.git"),
            Provider = RepositoryProvider.GitHub
        }, new OrganisationId());

    private static Deployment NewDeployment() =>
        Deployment.Create(new ApplicationId(), new EnvironmentId(Guid.NewGuid()), new CommitId(new string('a', 40)),
            "test@example.com");

    private sealed class FixedScoreDriver(ScoreFile? scoreFile) : IScoreDriver
    {
        public Task<ScoreFile?> ParseAsync(Deployment deployment, Application application,
            CancellationToken cancellationToken) => Task.FromResult(scoreFile);
    }

    private sealed class FakeRunnerApiClient : IRunnerApiClient
    {
        public RunPlan Plan { get; } = new(TerraformTestData.NewContext(), [TerraformTestData.PlanInput()]);
        public ScoreSubmission? Submission { get; private set; }
        public List<RunCompletion> Completions { get; } = [];
        public Exception? PlanFailure { get; set; }
        public Exception? CompleteFailure { get; set; }

        public Task<RunDescriptor> GetRunAsync(CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<RunPlan> SubmitScoreAsync(ScoreSubmission submission, CancellationToken cancellationToken)
        {
            Submission = submission;
            return PlanFailure is null ? Task.FromResult(Plan) : Task.FromException<RunPlan>(PlanFailure);
        }

        public Task CompleteAsync(RunCompletion completion, CancellationToken cancellationToken)
        {
            Completions.Add(completion);
            return CompleteFailure is null ? Task.CompletedTask : Task.FromException(CompleteFailure);
        }
    }

    private sealed class RecordingProvisioner : IEngineProvisioner
    {
        public IReadOnlyList<RunInput>? Provisioned { get; private set; }
        public IReadOnlyList<RunInput>? Deleted { get; private set; }
        public RunContext? Context { get; private set; }
        public Exception? Failure { get; set; }

        public Task ProvisionAsync(IReadOnlyList<RunInput> inputs, RunContext context,
            CancellationToken cancellationToken = default)
        {
            Provisioned = inputs;
            Context = context;
            return Failure is null ? Task.CompletedTask : Task.FromException(Failure);
        }

        public Task DeleteAsync(IReadOnlyList<RunInput> inputs, RunContext context,
            CancellationToken cancellationToken = default)
        {
            Deleted = inputs;
            Context = context;
            return Failure is null ? Task.CompletedTask : Task.FromException(Failure);
        }
    }
}
