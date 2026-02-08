using System.Diagnostics;
using System.Text;

namespace Orchitect.Engine.Infrastructure.CommandLine;

public sealed class CommandLineBuilder
{
    private readonly ProcessStartInfo _startInfo;

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

    public CommandLineBuilder WithWorkingDirectory(string workingDirectory)
    {
        _startInfo.WorkingDirectory = workingDirectory;
        return this;
    }

    public async Task<CommandLineResult> ExecuteAsync()
    {
        using var process = new Process();
        process.StartInfo = _startInfo;
        process.Start();

        var stdOutTask = process.StandardOutput.ReadToEndAsync();
        var stdErrTask = process.StandardError.ReadToEndAsync();

        await process.WaitForExitAsync();

        var stdOut = await stdOutTask;
        var stdErr = await stdErrTask;

        return new CommandLineResult(stdOut, stdErr, process.ExitCode);
    }

    public async Task<CommandLineResult> ExecuteStreamAsync()
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
                Console.Error.WriteLine(args.Data); // optional: live logging
            }
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        await process.WaitForExitAsync();

        return new CommandLineResult(stdOut.ToString(), stdErr.ToString(), process.ExitCode);
    }
}