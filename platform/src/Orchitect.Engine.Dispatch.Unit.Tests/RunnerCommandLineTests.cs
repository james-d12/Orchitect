using System.Diagnostics;

namespace Orchitect.Engine.Dispatch.Unit.Tests;

public sealed class RunnerCommandLineTests
{
    private static readonly string RunnerPath = Path.Combine(AppContext.BaseDirectory, "Orchitect.Runner.dll");

    [Theory]
    [InlineData]
    [InlineData("SecretProvider__Type", "HashicorpVault")]
    [InlineData("SecretProvider__Type", "AzureKeyVault")]
    [InlineData("ORCHITECT_API_URL", "not-a-url")]
    public async Task Help_WithoutValidConfiguration_PrintsUsage(params string[] environment)
    {
        var result = await RunAsync(["--help"], environment);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("--run-id", result.Output);
        Assert.Empty(result.Error);
    }

    [Fact]
    public async Task MissingRunId_WithoutConfiguration_ReportsArgumentError()
    {
        var result = await RunAsync([], []);

        Assert.Equal(1, result.ExitCode);
        Assert.Contains("Option '--run-id' is required.", result.Error);
        Assert.DoesNotContain("Exception", result.Error);
    }

    private static async Task<(int ExitCode, string Output, string Error)> RunAsync(
        string[] arguments,
        string[] environment)
    {
        var workingDirectory = Directory.CreateTempSubdirectory("orchitect-runner-cli-");

        try
        {
            var startInfo = new ProcessStartInfo(Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") ?? "dotnet")
            {
                WorkingDirectory = workingDirectory.FullName,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            };
            startInfo.ArgumentList.Add(RunnerPath);
            foreach (var argument in arguments)
            {
                startInfo.ArgumentList.Add(argument);
            }

            startInfo.Environment.Clear();
            startInfo.Environment["PATH"] = Environment.GetEnvironmentVariable("PATH");
            for (var i = 0; i < environment.Length; i += 2)
            {
                startInfo.Environment[environment[i]] = environment[i + 1];
            }

            using var process = Process.Start(startInfo)!;
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));

            var output = process.StandardOutput.ReadToEndAsync(timeout.Token);
            var error = process.StandardError.ReadToEndAsync(timeout.Token);
            await process.WaitForExitAsync(timeout.Token);

            return (process.ExitCode, await output, await error);
        }
        finally
        {
            workingDirectory.Delete(recursive: true);
        }
    }
}
