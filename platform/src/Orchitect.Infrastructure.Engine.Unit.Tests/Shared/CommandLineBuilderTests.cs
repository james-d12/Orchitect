using System.Diagnostics;
using Orchitect.Infrastructure.Engine.Shared.CommandLine;

namespace Orchitect.Infrastructure.Engine.Unit.Tests.Shared;

public sealed class CommandLineBuilderTests : IDisposable
{
    private readonly string _directory = Directory.CreateTempSubdirectory("orchitect-cli-tests").FullName;

    private string ReadyFile => Path.Combine(_directory, "ready");
    private string InterruptedFile => Path.Combine(_directory, "interrupted");

    public void Dispose() => Directory.Delete(_directory, recursive: true);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Execute_Cancelled_SendsSigIntAndWaitsForGracefulExit(bool stream)
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        var builder = Shell($"trap 'touch {InterruptedFile}; exit 0' INT; touch {ReadyFile}; " +
                            "while true; do sleep 0.1; done");
        using var cancellation = new CancellationTokenSource();

        var execution = Execute(builder, stream, cancellation.Token);
        await WaitForReadyAsync();
        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => execution);
        Assert.True(File.Exists(InterruptedFile));
    }

    [Fact]
    public async Task Execute_ProcessIgnoresSigInt_IsKilledAfterGracePeriod()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        var builder = Shell($"trap '' INT; touch {ReadyFile}; while true; do sleep 0.1; done")
            .WithInterruptGracePeriod(TimeSpan.FromMilliseconds(500));
        using var cancellation = new CancellationTokenSource();

        var execution = builder.ExecuteAsync(cancellation.Token);
        await WaitForReadyAsync();
        var stopwatch = Stopwatch.StartNew();
        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => execution);
        Assert.InRange(stopwatch.Elapsed, TimeSpan.FromMilliseconds(400), TimeSpan.FromSeconds(10));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Execute_NotCancelled_ReturnsOutputAndExitCode(bool stream)
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        var result = await Execute(Shell("echo out; echo err >&2; exit 3"), stream, CancellationToken.None);

        Assert.Equal(3, result.ExitCode);
        Assert.Equal("out", result.StdOut.Trim());
        Assert.Equal("err", result.StdErr.Trim());
    }

    private static CommandLineBuilder Shell(string script) =>
        new CommandLineBuilder("sh").WithArguments(["-c", script]);

    private static Task<CommandLineResult> Execute(CommandLineBuilder builder, bool stream,
        CancellationToken cancellationToken) =>
        stream ? builder.ExecuteStreamAsync(cancellationToken) : builder.ExecuteAsync(cancellationToken);

    private async Task WaitForReadyAsync()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        while (!File.Exists(ReadyFile))
        {
            await Task.Delay(20, timeout.Token);
        }
    }
}
