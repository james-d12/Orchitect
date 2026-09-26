using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace Orchitect.Infrastructure.Engine.Shared.CommandLine;

public sealed class CommandLineBuilder
{
    private const int SigInt = 2;
    private static readonly TimeSpan DefaultInterruptGracePeriod = TimeSpan.FromMinutes(5);

    private readonly ProcessStartInfo _startInfo;
    private TimeSpan _interruptGracePeriod = DefaultInterruptGracePeriod;

    public CommandLineBuilder(string fileName)
    {
        _startInfo = new ProcessStartInfo
        {
            FileName = fileName,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
    }

    public CommandLineBuilder WithArguments(string arguments)
    {
        _startInfo.Arguments = arguments;
        return this;
    }

    public CommandLineBuilder WithArguments(IEnumerable<string> arguments)
    {
        foreach (var argument in arguments)
        {
            _startInfo.ArgumentList.Add(argument);
        }

        return this;
    }

    public CommandLineBuilder WithWorkingDirectory(string workingDirectory)
    {
        _startInfo.WorkingDirectory = workingDirectory;
        return this;
    }

    /// <summary>
    /// Sets how long a cancelled process has to exit after SIGINT before it is killed.
    /// </summary>
    public CommandLineBuilder WithInterruptGracePeriod(TimeSpan gracePeriod)
    {
        _interruptGracePeriod = gracePeriod;
        return this;
    }

    /// <summary>
    /// Runs the process and buffers its output. On cancellation the process is sent SIGINT, given the
    /// interrupt grace period to exit, then killed, and an <see cref="OperationCanceledException"/> is thrown.
    /// </summary>
    public async Task<CommandLineResult> ExecuteAsync(CancellationToken cancellationToken = default)
    {
        using var process = new Process();
        process.StartInfo = _startInfo;
        process.Start();

        var stdOutTask = process.StandardOutput.ReadToEndAsync();
        var stdErrTask = process.StandardError.ReadToEndAsync();

        await WaitForExitAsync(process, cancellationToken);

        var stdOut = await stdOutTask;
        var stdErr = await stdErrTask;

        return new CommandLineResult(stdOut, stdErr, process.ExitCode);
    }

    /// <summary>
    /// Runs the process and streams its output to the console. Cancellation behaves as in
    /// <see cref="ExecuteAsync"/>.
    /// </summary>
    public async Task<CommandLineResult> ExecuteStreamAsync(CancellationToken cancellationToken = default)
    {
        using var process = new Process();
        process.StartInfo = _startInfo;
        process.EnableRaisingEvents = true;

        var stdOut = new StringBuilder();
        var stdErr = new StringBuilder();

        process.OutputDataReceived += (sender, args) =>
        {
            if (args.Data != null)
            {
                stdOut.AppendLine(args.Data);
                Console.WriteLine(args.Data);
            }
        };

        process.ErrorDataReceived += (sender, args) =>
        {
            if (args.Data != null)
            {
                stdErr.AppendLine(args.Data);
                Console.Error.WriteLine(args.Data);
            }
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        await WaitForExitAsync(process, cancellationToken);

        return new CommandLineResult(stdOut.ToString(), stdErr.ToString(), process.ExitCode);
    }

    private async Task WaitForExitAsync(Process process, CancellationToken cancellationToken)
    {
        await process.WaitForExitAsync(cancellationToken).ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);

        if (!cancellationToken.IsCancellationRequested)
        {
            return;
        }

        if (!process.HasExited)
        {
            Interrupt(process);

            using var gracePeriod = new CancellationTokenSource(_interruptGracePeriod);
            await process.WaitForExitAsync(gracePeriod.Token).ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);

            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                await process.WaitForExitAsync();
            }
        }

        throw new OperationCanceledException(cancellationToken);
    }

    private static void Interrupt(Process process)
    {
        if (OperatingSystem.IsWindows() || SendSignal(process.Id, SigInt) != 0)
        {
            process.Kill(entireProcessTree: true);
        }
    }

    [DllImport("libc", EntryPoint = "kill")]
    private static extern int SendSignal(int processId, int signal);
}
