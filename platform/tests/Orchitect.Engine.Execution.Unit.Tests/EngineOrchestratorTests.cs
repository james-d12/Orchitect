using Microsoft.Extensions.Logging.Abstractions;
using Orchitect.Engine.Contracts.Runner;
using Orchitect.Engine.Contracts.Runner.Api;
using Orchitect.Engine.Contracts.Score;
using Orchitect.Engine.Execution.Configuration.Score;
using Orchitect.Engine.Execution.Provisioner;
using Orchitect.Engine.Execution.RunnerApi;
using Orchitect.Engine.Execution.Secret;
using Orchitect.Engine.Execution.Unit.Tests.Terraform;

namespace Orchitect.Engine.Execution.Unit.Tests;

public sealed class EngineOrchestratorTests
{
    private readonly RecordingProvisioner _provisioner = new();
    private readonly FakeRunnerApiClient _runnerApi = new();
    private readonly RecordingSecretEnvironmentLoader _secrets = new();
    private FixedScoreDriver? _scoreDriver;

    [Fact]
    public async Task RunAsync_ProvisionRun_SubmitsScoreProvisionsPlanAndReportsSuccess()
    {
        var scoreFile = Score();

        await CreateOrchestrator(scoreFile).RunAsync(CancellationToken.None);

        Assert.Same(_runnerApi.Run, _scoreDriver?.Run);
        Assert.Same(scoreFile, _runnerApi.Submission?.ScoreFile);
        Assert.True(_secrets.Loaded);
        Assert.Equal(_runnerApi.Plan.Inputs, _provisioner.Provisioned);
        Assert.Same(_runnerApi.Plan.Context, _provisioner.Context);
        Assert.Null(_provisioner.Deleted);
        Assert.Equal(new RunCompletion(RunOutcome.Succeeded, null), Assert.Single(_runnerApi.Completions));
    }

    [Fact]
    public async Task RunAsync_DestroyRun_DeletesPlanAndReportsSuccess()
    {
        _runnerApi.Run = _runnerApi.Run with { Operation = RunnerOperation.Destroy };

        await CreateOrchestrator(Score()).RunAsync(CancellationToken.None);

        Assert.Equal(_runnerApi.Plan.Inputs, _provisioner.Deleted);
        Assert.Null(_provisioner.Provisioned);
        Assert.Equal(RunOutcome.Succeeded, Assert.Single(_runnerApi.Completions).Outcome);
    }

    [Fact]
    public async Task RunAsync_ProvisionFails_ReportsFailureAndRethrows()
    {
        _provisioner.Failure = new InvalidOperationException("terraform apply failed");

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CreateOrchestrator(Score()).RunAsync(CancellationToken.None));

        Assert.Same(_provisioner.Failure, exception);
        Assert.Equal(new RunCompletion(RunOutcome.Failed, "terraform apply failed"),
            Assert.Single(_runnerApi.Completions));
    }

    [Fact]
    public async Task RunAsync_DeleteFails_ReportsFailure()
    {
        _runnerApi.Run = _runnerApi.Run with { Operation = RunnerOperation.Destroy };
        _provisioner.Failure = new InvalidOperationException("terraform destroy failed");

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CreateOrchestrator(Score()).RunAsync(CancellationToken.None));

        Assert.Equal(RunOutcome.Failed, Assert.Single(_runnerApi.Completions).Outcome);
    }

    [Fact]
    public async Task RunAsync_ScoreFileMissing_ReportsFailureWithoutPlanning()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CreateOrchestrator(null).RunAsync(CancellationToken.None));

        Assert.Null(_runnerApi.Submission);
        Assert.Null(_provisioner.Provisioned);
        Assert.Equal(RunOutcome.Failed, Assert.Single(_runnerApi.Completions).Outcome);
    }

    [Fact]
    public async Task RunAsync_RunNotFetched_ReportsFailureWithoutParsing()
    {
        _runnerApi.RunFailure = new HttpRequestException("503 Service Unavailable");

        await Assert.ThrowsAsync<HttpRequestException>(() =>
            CreateOrchestrator(Score()).RunAsync(CancellationToken.None));

        Assert.Null(_scoreDriver?.Run);
        Assert.Null(_runnerApi.Submission);
        Assert.Equal(new RunCompletion(RunOutcome.Failed, "503 Service Unavailable"),
            Assert.Single(_runnerApi.Completions));
    }

    [Fact]
    public async Task RunAsync_SecretsNotLoaded_ReportsFailureWithoutProvisioning()
    {
        _secrets.Failure = new InvalidOperationException("Key Vault unavailable");

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CreateOrchestrator(Score()).RunAsync(CancellationToken.None));

        Assert.Null(_provisioner.Provisioned);
        Assert.Equal(new RunCompletion(RunOutcome.Failed, "Key Vault unavailable"),
            Assert.Single(_runnerApi.Completions));
    }

    [Fact]
    public async Task RunAsync_PlanRejected_ReportsFailureWithoutLoadingSecretsOrProvisioning()
    {
        _runnerApi.PlanFailure = new HttpRequestException("400 Bad Request");

        await Assert.ThrowsAsync<HttpRequestException>(() =>
            CreateOrchestrator(Score()).RunAsync(CancellationToken.None));

        Assert.False(_secrets.Loaded);
        Assert.Null(_provisioner.Provisioned);
        Assert.Equal(new RunCompletion(RunOutcome.Failed, "400 Bad Request"), Assert.Single(_runnerApi.Completions));
    }

    [Fact]
    public async Task RunAsync_FailureReportFails_RethrowsTheOriginalFailure()
    {
        _provisioner.Failure = new InvalidOperationException("terraform apply failed");
        _runnerApi.CompleteFailure = new HttpRequestException("API unavailable");

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CreateOrchestrator(Score()).RunAsync(CancellationToken.None));

        Assert.Same(_provisioner.Failure, exception);
    }

    private EngineOrchestrator CreateOrchestrator(ScoreFile? scoreFile)
    {
        _scoreDriver = new FixedScoreDriver(scoreFile);
        return new EngineOrchestrator(NullLogger<EngineOrchestrator>.Instance, _scoreDriver, _runnerApi, _secrets,
            _provisioner);
    }

    private static ScoreFile Score() => new()
    {
        ApiVersion = "score.dev/v1b1",
        Metadata = new ScoreMetadata { Name = "orders" },
        Resources = new Dictionary<string, ScoreResource>
        {
            ["storage"] = new() { Type = "azure-storage-account" }
        }
    };

    private sealed class FixedScoreDriver(ScoreFile? scoreFile) : IScoreDriver
    {
        public RunDescriptor? Run { get; private set; }

        public Task<ScoreFile?> ParseAsync(RunDescriptor run, CancellationToken cancellationToken)
        {
            Run = run;
            return Task.FromResult(scoreFile);
        }
    }

    private sealed class RecordingSecretEnvironmentLoader : ISecretEnvironmentLoader
    {
        public bool Loaded { get; private set; }
        public Exception? Failure { get; set; }

        public Task LoadAsync(CancellationToken cancellationToken = default)
        {
            Loaded = Failure is null;
            return Failure is null ? Task.CompletedTask : Task.FromException(Failure);
        }
    }

    private sealed class FakeRunnerApiClient : IRunnerApiClient
    {
        public RunDescriptor Run { get; set; } = new(Guid.NewGuid(), RunnerOperation.Provision,
            new Uri("https://example.com/orders.git"), new string('a', 40), Guid.NewGuid(), Guid.NewGuid());
        public RunPlan Plan { get; } = new(TerraformTestData.NewContext(), [TerraformTestData.PlanInput()]);
        public ScoreSubmission? Submission { get; private set; }
        public List<RunCompletion> Completions { get; } = [];
        public Exception? RunFailure { get; set; }
        public Exception? PlanFailure { get; set; }
        public Exception? CompleteFailure { get; set; }

        public Task<RunDescriptor> GetRunAsync(CancellationToken cancellationToken) =>
            RunFailure is null ? Task.FromResult(Run) : Task.FromException<RunDescriptor>(RunFailure);

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
