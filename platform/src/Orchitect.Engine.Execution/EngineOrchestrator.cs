using Microsoft.Extensions.Logging;
using Orchitect.Common.Observability;
using Orchitect.Domain.Engine.Application;
using Orchitect.Domain.Engine.Deployment;
using Orchitect.Engine.Contracts.Runner.Api;
using Orchitect.Engine.Contracts.Score;
using Orchitect.Engine.Execution.Configuration.Score;
using Orchitect.Engine.Execution.Provisioner;
using Orchitect.Engine.Execution.RunnerApi;

namespace Orchitect.Engine.Execution;

public interface IEngineOrchestrator
{
    /// <summary>
    /// Parses the deployment's score file, submits it for a plan, provisions the planned resources and reports
    /// the outcome.
    /// </summary>
    Task StartAsync(Application application, Deployment deployment, CancellationToken cancellationToken);

    /// <summary>
    /// Destroys a deployment's resources the same way as <see cref="StartAsync"/>, using the same project and state.
    /// </summary>
    Task DestroyAsync(Application application, Deployment deployment, CancellationToken cancellationToken);
}

public sealed class EngineOrchestrator : IEngineOrchestrator
{
    private readonly ILogger<EngineOrchestrator> _logger;
    private readonly IScoreDriver _scoreDriver;
    private readonly IRunnerApiClient _runnerApiClient;
    private readonly IEngineProvisioner _engineProvisioner;

    public EngineOrchestrator(ILogger<EngineOrchestrator> logger, IScoreDriver scoreDriver,
        IRunnerApiClient runnerApiClient, IEngineProvisioner engineProvisioner)
    {
        _logger = logger;
        _scoreDriver = scoreDriver;
        _runnerApiClient = runnerApiClient;
        _engineProvisioner = engineProvisioner;
    }

    public Task StartAsync(Application application, Deployment deployment, CancellationToken cancellationToken) =>
        RunAsync(application, deployment, "provisioning", _engineProvisioner.ProvisionAsync, cancellationToken);

    public Task DestroyAsync(Application application, Deployment deployment, CancellationToken cancellationToken) =>
        RunAsync(application, deployment, "destroying", _engineProvisioner.DeleteAsync, cancellationToken);

    private async Task RunAsync(Application application, Deployment deployment, string operation,
        Func<IReadOnlyList<RunInput>, RunContext, CancellationToken, Task> execute,
        CancellationToken cancellationToken)
    {
        using var activity = Tracing.StartActivity();

        try
        {
            ScoreFile scoreFile = await _scoreDriver.ParseAsync(deployment, application, cancellationToken)
                                  ?? throw new InvalidOperationException("Unable to find or parse the score file.");

            var plan = await _runnerApiClient.SubmitScoreAsync(new ScoreSubmission(scoreFile), cancellationToken);

            _logger.LogInformation("Running {Operation} for {InputCount} resources of the score file", operation,
                plan.Inputs.Count);

            await execute(plan.Inputs, plan.Context, cancellationToken);
        }
        catch (Exception exception)
        {
            activity?.RecordException(exception);
            _logger.LogError(exception, "An error occured while {Operation} the score file.", operation);
            await ReportFailureAsync(exception);
            throw;
        }

        await _runnerApiClient.CompleteAsync(new RunCompletion(RunOutcome.Succeeded, null), cancellationToken);
    }

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
