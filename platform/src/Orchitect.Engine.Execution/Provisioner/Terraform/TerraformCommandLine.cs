using Orchitect.Engine.Execution.Shared.CommandLine;

namespace Orchitect.Engine.Execution.Provisioner.Terraform;

public interface ITerraformCommandLine
{
    Task<CommandLineResult> RunTerraformJsonOutput(string executeDirectory, CancellationToken cancellationToken = default);
    Task<CommandLineResult> RunInitAsync(string executeDirectory, string? backendConfigFile,
        CancellationToken cancellationToken = default);
    Task<CommandLineResult> RunValidateAsync(string executeDirectory, CancellationToken cancellationToken = default);
    Task<CommandLineResult> RunPlanDestroyAsync(string executeDirectory, string planFileOutput, CancellationToken cancellationToken = default);
    Task<CommandLineResult> RunPlanAsync(string executeDirectory, string planFileOutput, CancellationToken cancellationToken = default);

    /// <summary>
    /// Applies a saved plan file. Also used to apply destroy plans.
    /// </summary>
    Task<CommandLineResult> RunApplyAsync(string executeDirectory, string planFile, CancellationToken cancellationToken = default);
}

public sealed class TerraformCommandLine : ITerraformCommandLine
{
    private const string TerraformCommand = "terraform";
    private const string LockTimeout = "-lock-timeout=5m";
    private const string NoInput = "-input=false";
    private const string NoColor = "-no-color";

    public async Task<CommandLineResult> RunTerraformJsonOutput(string executeDirectory, CancellationToken cancellationToken = default) =>
        await new CommandLineBuilder("terraform-config-inspect")
            .WithArguments(["--json", "."])
            .WithWorkingDirectory(executeDirectory)
            .ExecuteAsync(cancellationToken);

    public async Task<CommandLineResult> RunInitAsync(string executeDirectory,
        string? backendConfigFile, CancellationToken cancellationToken = default) =>
        await new CommandLineBuilder(TerraformCommand)
            .WithArguments([
                "init",
                NoInput,
                NoColor,
                ..(backendConfigFile is null ? Array.Empty<string>() : [$"-backend-config={backendConfigFile}"])
            ])
            .WithWorkingDirectory(executeDirectory)
            .ExecuteAsync(cancellationToken);

    public async Task<CommandLineResult> RunValidateAsync(string executeDirectory, CancellationToken cancellationToken = default) =>
        await new CommandLineBuilder(TerraformCommand)
            .WithArguments(["validate", NoColor])
            .WithWorkingDirectory(executeDirectory)
            .ExecuteAsync(cancellationToken);

    public async Task<CommandLineResult> RunPlanDestroyAsync(string executeDirectory, string planFileOutput, CancellationToken cancellationToken = default) =>
        await new CommandLineBuilder(TerraformCommand)
            .WithArguments([
                "plan", "-detailed-exitcode", NoInput, NoColor, LockTimeout, "-destroy",
                $"-out={planFileOutput}"
            ])
            .WithWorkingDirectory(executeDirectory)
            .ExecuteStreamAsync(cancellationToken);

    public async Task<CommandLineResult> RunPlanAsync(string executeDirectory, string planFileOutput, CancellationToken cancellationToken = default) =>
        await new CommandLineBuilder(TerraformCommand)
            .WithArguments([
                "plan", "-detailed-exitcode", NoInput, NoColor, LockTimeout, $"-out={planFileOutput}"
            ])
            .WithWorkingDirectory(executeDirectory)
            .ExecuteStreamAsync(cancellationToken);

    public async Task<CommandLineResult> RunApplyAsync(string executeDirectory, string planFile, CancellationToken cancellationToken = default) =>
        await new CommandLineBuilder(TerraformCommand)
            .WithArguments(["apply", "-auto-approve", NoInput, NoColor, LockTimeout, planFile])
            .WithWorkingDirectory(executeDirectory)
            .ExecuteStreamAsync(cancellationToken);
}
