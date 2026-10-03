using Microsoft.Extensions.Logging;
using Orchitect.Engine.Contracts.Runner.Api;

namespace Orchitect.Engine.Execution.RunnerApi;

public interface IRunCompletionReporter
{
    /// <summary>
    /// Runs the work and reports its outcome to the API, then rethrows any failure so the exit code matches. A report
    /// that can't be delivered is only logged, because the API then falls back to the exit code.
    /// </summary>
    Task RunAsync(Func<CancellationToken, Task> work, CancellationToken cancellationToken);
}

public sealed class RunCompletionReporter : IRunCompletionReporter
{
    private readonly IRunnerApiClient _client;
    private readonly ILogger<RunCompletionReporter> _logger;

    public RunCompletionReporter(IRunnerApiClient client, ILogger<RunCompletionReporter> logger)
    {
        _client = client;
        _logger = logger;
    }

    public async Task RunAsync(Func<CancellationToken, Task> work, CancellationToken cancellationToken)
    {
        try
        {
            await work(cancellationToken);
        }
        catch (Exception exception)
        {
            await ReportAsync(new RunCompletion(RunOutcome.Failed, exception.Message));
            throw;
        }

        await ReportAsync(new RunCompletion(RunOutcome.Succeeded, null));
    }

    private async Task ReportAsync(RunCompletion completion)
    {
        try
        {
            await _client.CompleteAsync(completion, CancellationToken.None);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception,
                "Could not report the run as {Outcome}. The API will use the runner's exit code instead.",
                completion.Outcome);
        }
    }
}
