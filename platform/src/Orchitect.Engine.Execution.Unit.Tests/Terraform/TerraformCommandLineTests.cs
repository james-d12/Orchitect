using Orchitect.Engine.Execution.Provisioner.Terraform;
using Orchitect.Engine.Execution.Shared.CommandLine;

namespace Orchitect.Engine.Execution.Unit.Tests.Terraform;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class PathMutatingCollection
{
    public const string Name = "Path mutating";
}

[Collection(PathMutatingCollection.Name)]
public sealed class TerraformCommandLineTests : IDisposable
{
    private readonly string _directory = Directory.CreateTempSubdirectory("orchitect-terraform-cli-tests").FullName;
    private readonly string? _originalPath = System.Environment.GetEnvironmentVariable("PATH");
    private readonly TerraformCommandLine _commandLine = new();

    private string BinDirectory => Path.Combine(_directory, "bin");
    private string WorkingDirectory => Path.Combine(_directory, "work");

    public TerraformCommandLineTests()
    {
        Directory.CreateDirectory(BinDirectory);
        Directory.CreateDirectory(WorkingDirectory);

        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        foreach (var name in new[] { "terraform", "terraform-config-inspect" })
        {
            var script = Path.Combine(BinDirectory, name);
            File.WriteAllText(script, "#!/bin/sh\nprintf '%s\\n' \"$PWD\" \"$@\"\n");
            File.SetUnixFileMode(script,
                UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }

        System.Environment.SetEnvironmentVariable("PATH", $"{BinDirectory}:{_originalPath}");
    }

    public void Dispose()
    {
        System.Environment.SetEnvironmentVariable("PATH", _originalPath);
        Directory.Delete(_directory, recursive: true);
    }

    [Fact]
    public async Task RunTerraformJsonOutput_InspectsTheWorkingDirectory()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        var result = await _commandLine.RunTerraformJsonOutput(WorkingDirectory);

        AssertInvocation(result, "--json", ".");
    }

    [Fact]
    public async Task RunInitAsync_NoBackendConfig_OmitsBackendConfigArgument()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        var result = await _commandLine.RunInitAsync(WorkingDirectory, null);

        AssertInvocation(result, "init", "-input=false", "-no-color");
    }

    [Fact]
    public async Task RunInitAsync_BackendConfig_PassesBackendConfigArgument()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        var result = await _commandLine.RunInitAsync(WorkingDirectory, "backend.tfbackend");

        AssertInvocation(result, "init", "-input=false", "-no-color", "-backend-config=backend.tfbackend");
    }

    [Fact]
    public async Task RunValidateAsync_RunsValidate()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        var result = await _commandLine.RunValidateAsync(WorkingDirectory);

        AssertInvocation(result, "validate", "-no-color");
    }

    [Fact]
    public async Task RunPlanAsync_WritesPlanToOutputFile()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        var result = await _commandLine.RunPlanAsync(WorkingDirectory, "plan.tfplan");

        AssertInvocation(result, "plan", "-detailed-exitcode", "-input=false", "-no-color", "-lock-timeout=5m",
            "-out=plan.tfplan");
    }

    [Fact]
    public async Task RunPlanDestroyAsync_WritesDestroyPlanToOutputFile()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        var result = await _commandLine.RunPlanDestroyAsync(WorkingDirectory, "destroy.tfplan");

        AssertInvocation(result, "plan", "-detailed-exitcode", "-input=false", "-no-color", "-lock-timeout=5m",
            "-destroy", "-out=destroy.tfplan");
    }

    [Fact]
    public async Task RunApplyAsync_AppliesThePlanFile()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        var result = await _commandLine.RunApplyAsync(WorkingDirectory, "plan.tfplan");

        AssertInvocation(result, "apply", "-auto-approve", "-input=false", "-no-color", "-lock-timeout=5m",
            "plan.tfplan");
    }

    private void AssertInvocation(CommandLineResult result, params string[] expectedArguments)
    {
        Assert.Equal(0, result.ExitCode);
        Assert.Equal([WorkingDirectory, .. expectedArguments],
            result.StdOut.Split('\n', StringSplitOptions.RemoveEmptyEntries));
    }
}
