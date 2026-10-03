using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Orchitect.Engine.Contracts.Runner.Api;
using Orchitect.Engine.Execution.RunnerApi;

namespace Orchitect.Engine.Execution.Unit.Tests.RunnerApi;

public sealed class RunCompletionReporterTests
{
    private readonly IRunnerApiClient _client = Substitute.For<IRunnerApiClient>();
    private readonly RunCompletionReporter _reporter;

    public RunCompletionReporterTests()
    {
        _reporter = new RunCompletionReporter(_client, NullLogger<RunCompletionReporter>.Instance);
    }

    [Fact]
    public async Task RunAsync_WorkSucceeds_ReportsSucceeded()
    {
        await _reporter.RunAsync(_ => Task.CompletedTask, CancellationToken.None);

        await _client.Received(1).CompleteAsync(new RunCompletion(RunOutcome.Succeeded, null),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RunAsync_WorkFails_ReportsFailedWithTheMessageAndRethrows()
    {
        var failure = new InvalidOperationException("terraform apply failed");

        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _reporter.RunAsync(_ => Task.FromException(failure), CancellationToken.None));

        Assert.Same(failure, thrown);
        await _client.Received(1).CompleteAsync(new RunCompletion(RunOutcome.Failed, "terraform apply failed"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RunAsync_ReportFails_KeepsTheWorkResult()
    {
        _client.CompleteAsync(Arg.Any<RunCompletion>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Connection refused."));

        await _reporter.RunAsync(_ => Task.CompletedTask, CancellationToken.None);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _reporter.RunAsync(_ => Task.FromException(new InvalidOperationException("boom")),
                CancellationToken.None));
    }

    [Fact]
    public async Task RunAsync_Cancelled_StillReportsFailed()
    {
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            _reporter.RunAsync(ct => Task.Delay(Timeout.InfiniteTimeSpan, ct), cancellation.Token));

        await _client.Received(1).CompleteAsync(Arg.Is<RunCompletion>(c => c.Outcome == RunOutcome.Failed),
            CancellationToken.None);
    }
}
