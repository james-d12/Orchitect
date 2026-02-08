using Conductor.Engine.Infrastructure.CommandLine;

namespace Conductor.Engine.Infrastructure.Terraform;

public interface ITerraformCommandLine
{
    Task<CommandLineResult> RunTerraformJsonOutput(string executeDirectory);
    Task<CommandLineResult> RunInitAsync(string executeDirectory);
    Task<CommandLineResult> RunValidateAsync(string executeDirectory);
    Task<CommandLineResult> RunPlanDestroyAsync(string executeDirectory);
    Task<CommandLineResult> RunPlanAsync(string executeDirectory, string planFileOutput);
    Task<CommandLineResult> RunApplyAsync(string executeDirectory, string planFile);
    Task<CommandLineResult> RunDestroyAsync(string executeDirectory);
}

public sealed class TerraformCommandLine : ITerraformCommandLine
{
    public async Task<CommandLineResult> RunTerraformJsonOutput(string executeDirectory) =>
        await new CommandLineBuilder("terraform-config-inspect")
            .WithArguments("--json .")
            .WithWorkingDirectory(executeDirectory)
            .ExecuteAsync();

    public async Task<CommandLineResult> RunInitAsync(string executeDirectory) =>
        await new CommandLineBuilder("terraform")
            .WithArguments("init")
            .WithWorkingDirectory(executeDirectory)
            .ExecuteAsync();

    public async Task<CommandLineResult> RunValidateAsync(string executeDirectory) =>
        await new CommandLineBuilder("terraform")
            .WithArguments("validate")
            .WithWorkingDirectory(executeDirectory)
            .ExecuteAsync();

    public async Task<CommandLineResult> RunPlanDestroyAsync(string executeDirectory) =>
        await new CommandLineBuilder("terraform")
            .WithArguments("plan -detailed-exitcode -input=false -destroy -out=plan-destroy.tfplan")
            .WithWorkingDirectory(executeDirectory)
            .ExecuteAsync();

    public async Task<CommandLineResult> RunPlanAsync(string executeDirectory, string planFileOutput) =>
        await new CommandLineBuilder("terraform")
            .WithArguments($"plan -detailed-exitcode -input=false -out={planFileOutput}")
            .WithWorkingDirectory(executeDirectory)
            .ExecuteAsync();

    public async Task<CommandLineResult> RunApplyAsync(string executeDirectory, string planFile) =>
        await new CommandLineBuilder("terraform")
            .WithArguments($"apply -auto-approve {planFile}")
            .WithWorkingDirectory(executeDirectory)
            .ExecuteStreamAsync();

    public async Task<CommandLineResult> RunDestroyAsync(string executeDirectory) =>
        await new CommandLineBuilder("terraform")
            .WithArguments("apply -destroy plan-destroy.tfplan")
            .WithWorkingDirectory(executeDirectory)
            .ExecuteStreamAsync();
}