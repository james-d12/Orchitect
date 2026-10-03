using Orchitect.Engine.Contracts.Runner.Api;

namespace Orchitect.Engine.Dispatch.Completion;

/// <summary>
/// The result of a run, from the runner's report, the container's exit code or the executor.
/// </summary>
public sealed record RunResult(RunOutcome Outcome, string? ErrorSummary, long? ExitCode = null, string? RunnerId = null)
{
    public const string ReportedFailureSummary = "The runner reported a failure.";

    public static RunResult FromReport(RunCompletion completion) =>
        completion.Outcome == RunOutcome.Succeeded
            ? new RunResult(RunOutcome.Succeeded, null)
            : Failure(string.IsNullOrWhiteSpace(completion.ErrorSummary)
                ? ReportedFailureSummary
                : completion.ErrorSummary);

    public static RunResult FromExitCode(long exitCode, string? runnerId = null) =>
        exitCode == 0
            ? new RunResult(RunOutcome.Succeeded, null, exitCode, runnerId)
            : Failure($"The runner exited with code {exitCode}.", exitCode, runnerId);

    public static RunResult Failure(string errorSummary, long? exitCode = null, string? runnerId = null) =>
        new(RunOutcome.Failed, errorSummary, exitCode, runnerId);
}
