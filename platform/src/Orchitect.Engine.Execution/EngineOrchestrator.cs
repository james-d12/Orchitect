using Microsoft.Extensions.Logging;
using Orchitect.Common.Observability;
using Orchitect.Engine.Contracts.Runner;
using Orchitect.Engine.Contracts.Runner.Api;
using Orchitect.Engine.Contracts.Score;
using Orchitect.Engine.Execution.Configuration.Score;
using Orchitect.Engine.Execution.Provisioner;
using Orchitect.Engine.Execution.RunnerApi;
using Orchitect.Engine.Execution.Secret;

namespace Orchitect.Engine.Execution;

public interface IEngineOrchestrator
{
    /// <summary>
    /// Fetches this runner's run, parses its score file, submits it for a plan, loads the mapped secrets, provisions
    /// or destroys the planned resources and reports the outcome.
    /// </summary>
    Task RunAsync(CancellationToken cancellationToken);
}

public sealed class EngineOrchestrator : IEngineOrchestrator
{
    private readonly ILogger<EngineOrchestrator> _logger;
    private readonly IScoreDriver _scoreDriver;
    private readonly IRunnerApiClient _runnerApiClient;
    private readonly ISecretEnvironmentLoader _secretEnvironmentLoader;
    private readonly IEngineProvisioner _engineProvisioner;

    public EngineOrchestrator(ILogger<EngineOrchestrator> logger, IScoreDriver scoreDriver,
        IRunnerApiClient runnerApiClient, ISecretEnvironmentLoader secretEnvironmentLoader,
        IEngineProvisioner engineProvisioner)
    {
        _logger = logger;
        _scoreDriver = scoreDriver;
        _runnerApiClient = runnerApiClient;
        _secretEnvironmentLoader = secretEnvironmentLoader;
        _engineProvisioner = engineProvisioner;
    }

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        using var activity = Tracing.StartActivity();

        try
        {
            var run = await _runnerApiClient.GetRunAsync(cancellationToken);
            activity?.SetTag("orchitect.run.operation", run.Operation.ToString());

            ScoreFile scoreFile = await _scoreDriver.ParseAsync(run, cancellationToken)
                                  ?? throw new InvalidOperationException("Unable to find or parse the score file.");

            var plan = await _runnerApiClient.SubmitScoreAsync(new ScoreSubmission(scoreFile), cancellationToken);

            await _secretEnvironmentLoader.LoadAsync(cancellationToken);

            _logger.LogInformation("Running {Operation} for {InputCount} resources of the score file", run.Operation,
                plan.Inputs.Count);

            await ExecuteAsync(run.Operation, plan, cancellationToken);
        }
        catch (Exception exception)
        {
            activity?.RecordException(exception);
            _logger.LogError(exception, "An error occured while running the score file.");
            await ReportFailureAsync(exception);
            throw;
        }

        await _runnerApiClient.CompleteAsync(new RunCompletion(RunOutcome.Succeeded, null), cancellationToken);
    }

    private Task ExecuteAsync(RunnerOperation operation, RunPlan plan, CancellationToken cancellationToken) =>
        operation switch
        {
            RunnerOperation.Provision => _engineProvisioner.ProvisionAsync(plan.Inputs, plan.Context,
                cancellationToken),
            RunnerOperation.Destroy => _engineProvisioner.DeleteAsync(plan.Inputs, plan.Context, cancellationToken),
            _ => throw new InvalidOperationException($"Unsupported runner operation '{operation}'.")
        };

    private async Task ReportFailureAsync(Exception exception)
    {
        try
        {
            await _runnerApiClient.CompleteAsync(new RunCompletion(RunOutcome.Failed, exception.Message),
                CancellationToken.None);
        }
        catch (Exception reportException)
        {
            _logger.LogError(reportException, "Could not report the failed run.");
        }
    }
}
