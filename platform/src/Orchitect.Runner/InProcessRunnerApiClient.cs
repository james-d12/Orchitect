using Orchitect.Domain.Engine.Deployment;
using Orchitect.Engine.Contracts.Runner.Api;
using Orchitect.Engine.Dispatch.Completion;
using Orchitect.Engine.Dispatch.Plan;
using Orchitect.Engine.Execution.RunnerApi;

namespace Orchitect.Runner;

internal sealed class InProcessRunnerApiClient : IRunnerApiClient
{
    private readonly IRunPlanner _runPlanner;
    private readonly IRunCompleter _runCompleter;
    private readonly DeploymentRunId _runId;

    public InProcessRunnerApiClient(IRunPlanner runPlanner, IRunCompleter runCompleter, DeploymentRunId runId)
    {
        _runPlanner = runPlanner;
        _runCompleter = runCompleter;
        _runId = runId;
    }

    public Task<RunDescriptor> GetRunAsync(CancellationToken cancellationToken) =>
        throw new NotSupportedException("The runner reads its run from its arguments until it uses the runner API.");

    public Task<RunPlan> SubmitScoreAsync(ScoreSubmission submission, CancellationToken cancellationToken) =>
        _runPlanner.PlanAsync(_runId, submission.ScoreFile, cancellationToken);

    public Task CompleteAsync(RunCompletion completion, CancellationToken cancellationToken) =>
        _runCompleter.CompleteAsync(_runId, completion.Outcome, cancellationToken);
}
