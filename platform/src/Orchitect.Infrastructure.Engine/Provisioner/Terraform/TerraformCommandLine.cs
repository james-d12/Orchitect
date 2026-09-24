using Orchitect.Infrastructure.Engine.Shared.CommandLine;

namespace Orchitect.Infrastructure.Engine.Provisioner.Terraform;

public interface ITerraformCommandLine
{
    Task<CommandLineResult> RunTerraformJsonOutput(string executeDirectory);
    Task<CommandLineResult> RunInitAsync(string executeDirectory, IReadOnlyDictionary<string, string> backendConfig);
    Task<CommandLineResult> RunValidateAsync(string executeDirectory);
    Task<CommandLineResult> RunPlanDestroyAsync(string executeDirectory, string planFileOutput);
    Task<CommandLineResult> RunPlanAsync(string executeDirectory, string planFileOutput);
    Task<CommandLineResult> RunApplyAsync(string executeDirectory, string planFile);
    Task<CommandLineResult> RunDestroyAsync(string executeDirectory, string planFile);
}

public sealed class TerraformCommandLine : ITerraformCommandLine
{
    public async Task<CommandLineResult> RunTerraformJsonOutput(string executeDirectory) =>
        await new CommandLineBuilder("terraform-config-inspect")
            .WithArguments("--json .")
            .WithWorkingDirectory(executeDirectory)
            .ExecuteAsync();

    public async Task<CommandLineResult> RunInitAsync(string executeDirectory,
        IReadOnlyDictionary<string, string> backendConfig) =>
        await new CommandLineBuilder("terraform")
            .WithArguments([
                "init",
                "-input=false",
                "-no-color",
                ..backendConfig.Select(kvp => $"-backend-config={kvp.Key}={kvp.Value}")
            ])
            .WithWorkingDirectory(executeDirectory)
            .ExecuteAsync();

    public async Task<CommandLineResult> RunValidateAsync(string executeDirectory) =>
        await new CommandLineBuilder("terraform")
            .WithArguments("validate -no-color")
            .WithWorkingDirectory(executeDirectory)
            .ExecuteAsync();

    public async Task<CommandLineResult> RunPlanDestroyAsync(string executeDirectory, string planFileOutput) =>
        await new CommandLineBuilder("terraform")
            .WithArguments($"plan -detailed-exitcode -input=false -no-color -destroy -out={planFileOutput}")
            .WithWorkingDirectory(executeDirectory)
            .ExecuteStreamAsync();

    public async Task<CommandLineResult> RunPlanAsync(string executeDirectory, string planFileOutput) =>
        await new CommandLineBuilder("terraform")
            .WithArguments($"plan -detailed-exitcode -input=false -no-color -out={planFileOutput}")
            .WithWorkingDirectory(executeDirectory)
            .ExecuteStreamAsync();

    public async Task<CommandLineResult> RunApplyAsync(string executeDirectory, string planFile) =>
        await new CommandLineBuilder("terraform")
            .WithArguments($"apply -auto-approve -no-color {planFile}")
            .WithWorkingDirectory(executeDirectory)
            .ExecuteStreamAsync();

    public async Task<CommandLineResult> RunDestroyAsync(string executeDirectory, string planFile) =>
        await new CommandLineBuilder("terraform")
            .WithArguments($"apply -auto-approve -no-color {planFile}")
            .WithWorkingDirectory(executeDirectory)
            .ExecuteStreamAsync();
}