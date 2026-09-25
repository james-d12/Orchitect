using Orchitect.Infrastructure.Engine.Shared.CommandLine;

namespace Orchitect.Infrastructure.Engine.Provisioner.Terraform;

public interface ITerraformCommandLine
{
    Task<CommandLineResult> RunTerraformJsonOutput(string executeDirectory, CancellationToken cancellationToken = default);
    Task<CommandLineResult> RunInitAsync(string executeDirectory, IReadOnlyDictionary<string, string> backendConfig,
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

    public async Task<CommandLineResult> RunTerraformJsonOutput(string executeDirectory, CancellationToken cancellationToken = default) =>
        await new CommandLineBuilder("terraform-config-inspect")
            .WithArguments(["--json", "."])
            .WithWorkingDirectory(executeDirectory)
            .ExecuteAsync(cancellationToken);

    public async Task<CommandLineResult> RunInitAsync(string executeDirectory,
        IReadOnlyDictionary<string, string> backendConfig, CancellationToken cancellationToken = default) =>
        await new CommandLineBuilder(TerraformCommand)
            .WithArguments([
                "init",
                "-input=false",
                "-no-color",
                ..backendConfig.Select(kvp => $"-backend-config={kvp.Key}={kvp.Value}")
            ])
            .WithWorkingDirectory(executeDirectory)
            .ExecuteAsync(cancellationToken);

    public async Task<CommandLineResult> RunValidateAsync(string executeDirectory, CancellationToken cancellationToken = default) =>
        await new CommandLineBuilder(TerraformCommand)
            .WithArguments(["validate", "-no-color"])
            .WithWorkingDirectory(executeDirectory)
            .ExecuteAsync(cancellationToken);

    public async Task<CommandLineResult> RunPlanDestroyAsync(string executeDirectory, string planFileOutput, CancellationToken cancellationToken = default) =>
        await new CommandLineBuilder(TerraformCommand)
            .WithArguments([
                "plan", "-detailed-exitcode", "-input=false", "-no-color", LockTimeout, "-destroy",
                $"-out={planFileOutput}"
            ])
            .WithWorkingDirectory(executeDirectory)
            .ExecuteStreamAsync(cancellationToken);

    public async Task<CommandLineResult> RunPlanAsync(string executeDirectory, string planFileOutput, CancellationToken cancellationToken = default) =>
        await new CommandLineBuilder(TerraformCommand)
            .WithArguments([
                "plan", "-detailed-exitcode", "-input=false", "-no-color", LockTimeout, $"-out={planFileOutput}"
            ])
            .WithWorkingDirectory(executeDirectory)
            .ExecuteStreamAsync(cancellationToken);

    public async Task<CommandLineResult> RunApplyAsync(string executeDirectory, string planFile, CancellationToken cancellationToken = default) =>
        await new CommandLineBuilder(TerraformCommand)
            .WithArguments(["apply", "-auto-approve", "-input=false", "-no-color", LockTimeout, planFile])
            .WithWorkingDirectory(executeDirectory)
            .ExecuteStreamAsync(cancellationToken);
}
