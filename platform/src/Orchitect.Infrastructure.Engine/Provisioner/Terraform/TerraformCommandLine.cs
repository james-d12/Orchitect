using Orchitect.Infrastructure.Engine.Shared.CommandLine;

namespace Orchitect.Infrastructure.Engine.Provisioner.Terraform;

public interface ITerraformCommandLine
{
    Task<CommandLineResult> RunTerraformJsonOutput(string executeDirectory);
    Task<CommandLineResult> RunInitAsync(string executeDirectory, IReadOnlyDictionary<string, string> backendConfig);
    Task<CommandLineResult> RunValidateAsync(string executeDirectory);
    Task<CommandLineResult> RunPlanDestroyAsync(string executeDirectory, string planFileOutput);
    Task<CommandLineResult> RunPlanAsync(string executeDirectory, string planFileOutput);

    /// <summary>
    /// Applies a saved plan file. Also used to apply destroy plans.
    /// </summary>
    Task<CommandLineResult> RunApplyAsync(string executeDirectory, string planFile);
}

public sealed class TerraformCommandLine : ITerraformCommandLine
{
    private const string TerraformCommand = "terraform";
    private const string LockTimeout = "-lock-timeout=5m";

    public async Task<CommandLineResult> RunTerraformJsonOutput(string executeDirectory) =>
        await new CommandLineBuilder("terraform-config-inspect")
            .WithArguments(["--json", "."])
            .WithWorkingDirectory(executeDirectory)
            .ExecuteAsync();

    public async Task<CommandLineResult> RunInitAsync(string executeDirectory,
        IReadOnlyDictionary<string, string> backendConfig) =>
        await new CommandLineBuilder(TerraformCommand)
            .WithArguments([
                "init",
                "-input=false",
                "-no-color",
                ..backendConfig.Select(kvp => $"-backend-config={kvp.Key}={kvp.Value}")
            ])
            .WithWorkingDirectory(executeDirectory)
            .ExecuteAsync();

    public async Task<CommandLineResult> RunValidateAsync(string executeDirectory) =>
        await new CommandLineBuilder(TerraformCommand)
            .WithArguments(["validate", "-no-color"])
            .WithWorkingDirectory(executeDirectory)
            .ExecuteAsync();

    public async Task<CommandLineResult> RunPlanDestroyAsync(string executeDirectory, string planFileOutput) =>
        await new CommandLineBuilder(TerraformCommand)
            .WithArguments([
                "plan", "-detailed-exitcode", "-input=false", "-no-color", LockTimeout, "-destroy",
                $"-out={planFileOutput}"
            ])
            .WithWorkingDirectory(executeDirectory)
            .ExecuteStreamAsync();

    public async Task<CommandLineResult> RunPlanAsync(string executeDirectory, string planFileOutput) =>
        await new CommandLineBuilder(TerraformCommand)
            .WithArguments([
                "plan", "-detailed-exitcode", "-input=false", "-no-color", LockTimeout, $"-out={planFileOutput}"
            ])
            .WithWorkingDirectory(executeDirectory)
            .ExecuteStreamAsync();

    public async Task<CommandLineResult> RunApplyAsync(string executeDirectory, string planFile) =>
        await new CommandLineBuilder(TerraformCommand)
            .WithArguments(["apply", "-auto-approve", "-input=false", "-no-color", LockTimeout, planFile])
            .WithWorkingDirectory(executeDirectory)
            .ExecuteStreamAsync();
}
