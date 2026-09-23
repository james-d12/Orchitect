using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Orchitect.Infrastructure.Engine.Provisioner.Terraform;
using Orchitect.Infrastructure.Engine.Provisioner.Terraform.Models;
using Orchitect.Infrastructure.Engine.Shared.CommandLine;

namespace Orchitect.Infrastructure.Unit.Tests.Engine.Terraform;

public sealed class TerraformDriverTests
{
    [Fact]
    public async Task PlanAsync_ProvisionAndDestroy_InitAgainstTheSameBackendState()
    {
        var context = TerraformTestData.NewContext();
        var commandLine = new RecordingTerraformCommandLine();
        var driver = CreateDriver(commandLine);
        List<TerraformPlanInput> inputs = [TerraformTestData.PlanInput()];

        await driver.PlanAsync(inputs, context);
        await driver.PlanAsync(inputs, context, destroy: true);

        Assert.Equal(2, commandLine.InitBackendConfigs.Count);
        Assert.Equal(commandLine.InitBackendConfigs[0], commandLine.InitBackendConfigs[1]);
        Assert.Equal($"{context.ApplicationId}/{context.EnvironmentId}.tfstate",
            commandLine.InitBackendConfigs[0]["key"]);
    }

    [Fact]
    public async Task DestroyAsync_AppliesTheSavedDestroyPlan()
    {
        var commandLine = new RecordingTerraformCommandLine();
        var driver = CreateDriver(commandLine);

        var planResult = await driver.PlanAsync([TerraformTestData.PlanInput()], TerraformTestData.NewContext(),
            destroy: true);
        await driver.DestroyAsync(planResult);

        Assert.Equal(TerraformPlanResultState.Success, planResult.State);
        Assert.NotNull(commandLine.DestroyPlanFile);
        Assert.Equal(commandLine.PlanDestroyOutput, commandLine.DestroyPlanFile);
    }

    [Fact]
    public async Task ApplyAsync_ApplyExitsNonZero_Throws()
    {
        var driver = CreateDriver(new RecordingTerraformCommandLine { ApplyExitCode = 1 });
        var planResult = await driver.PlanAsync([TerraformTestData.PlanInput()], TerraformTestData.NewContext());

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => driver.ApplyAsync(planResult));

        Assert.Contains("apply error", exception.Message);
    }

    [Fact]
    public async Task DestroyAsync_DestroyExitsNonZero_Throws()
    {
        var driver = CreateDriver(new RecordingTerraformCommandLine { ApplyExitCode = 1 });
        var planResult = await driver.PlanAsync([TerraformTestData.PlanInput()], TerraformTestData.NewContext(),
            destroy: true);

        await Assert.ThrowsAsync<InvalidOperationException>(() => driver.DestroyAsync(planResult));
    }

    [Fact]
    public async Task ApplyAsync_PlanFailed_ThrowsWithoutApplying()
    {
        var commandLine = new RecordingTerraformCommandLine { PlanExitCode = (int)TerraformPlanResultExitCode.Errored };
        var driver = CreateDriver(commandLine);
        var planResult = await driver.PlanAsync([TerraformTestData.PlanInput()], TerraformTestData.NewContext());

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => driver.ApplyAsync(planResult));

        Assert.Equal(TerraformPlanResultState.PlanFailed, planResult.State);
        Assert.Contains("plan error", exception.Message);
        Assert.False(commandLine.ApplyCalled);
    }

    [Fact]
    public async Task ApplyAsync_InitFailed_ThrowsWithInitError()
    {
        var driver = CreateDriver(new RecordingTerraformCommandLine { InitExitCode = 1 });
        var planResult = await driver.PlanAsync([TerraformTestData.PlanInput()], TerraformTestData.NewContext());

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => driver.ApplyAsync(planResult));

        Assert.Equal(TerraformPlanResultState.InitFailed, planResult.State);
        Assert.Contains("init error", exception.Message);
    }

    [Fact]
    public async Task ApplyAsync_NoChanges_DoesNotApplyOrThrow()
    {
        var commandLine = new RecordingTerraformCommandLine
        {
            PlanExitCode = (int)TerraformPlanResultExitCode.NoChanges
        };
        var driver = CreateDriver(commandLine);
        var planResult = await driver.PlanAsync([TerraformTestData.PlanInput()], TerraformTestData.NewContext());

        await driver.ApplyAsync(planResult);

        Assert.Equal(TerraformPlanResultState.NoChanges, planResult.State);
        Assert.False(commandLine.ApplyCalled);
    }

    private static TerraformDriver CreateDriver(ITerraformCommandLine commandLine)
    {
        var projectBuilder = new TerraformProjectBuilder(NullLogger<TerraformProjectBuilder>.Instance,
            new TerraformRenderer(), Options.Create(TerraformTestData.AzureBackend()));

        return new TerraformDriver(NullLogger<TerraformDriver>.Instance, new AlwaysValidTerraformValidator(),
            commandLine, projectBuilder);
    }

    private sealed class AlwaysValidTerraformValidator : ITerraformValidator
    {
        public Task<Dictionary<TerraformPlanInput, TerraformValidationResult>> ValidateAsync(
            List<TerraformPlanInput> terraformPlanInputs) =>
            Task.FromResult(terraformPlanInputs.ToDictionary(input => input,
                TerraformValidationResult (_) => TerraformTestData.ValidResult()));
    }

    private sealed class RecordingTerraformCommandLine : ITerraformCommandLine
    {
        private static readonly CommandLineResult Success = new(string.Empty, string.Empty, 0);

        public int InitExitCode { get; init; }
        public int PlanExitCode { get; init; } = (int)TerraformPlanResultExitCode.ChangesNeeded;
        public int ApplyExitCode { get; init; }

        public List<IReadOnlyDictionary<string, string>> InitBackendConfigs { get; } = [];
        public string? PlanDestroyOutput { get; private set; }
        public string? DestroyPlanFile { get; private set; }
        public bool ApplyCalled { get; private set; }

        public Task<CommandLineResult> RunTerraformJsonOutput(string executeDirectory) => Task.FromResult(Success);

        public Task<CommandLineResult> RunInitAsync(string executeDirectory,
            IReadOnlyDictionary<string, string> backendConfig)
        {
            InitBackendConfigs.Add(backendConfig);
            return Task.FromResult(new CommandLineResult(string.Empty, "init error", InitExitCode));
        }

        public Task<CommandLineResult> RunValidateAsync(string executeDirectory) => Task.FromResult(Success);

        public Task<CommandLineResult> RunPlanDestroyAsync(string executeDirectory, string planFileOutput)
        {
            PlanDestroyOutput = planFileOutput;
            return Task.FromResult(new CommandLineResult(string.Empty, "plan error", PlanExitCode));
        }

        public Task<CommandLineResult> RunPlanAsync(string executeDirectory, string planFileOutput) =>
            Task.FromResult(new CommandLineResult(string.Empty, "plan error", PlanExitCode));

        public Task<CommandLineResult> RunApplyAsync(string executeDirectory, string planFile)
        {
            ApplyCalled = true;
            return Task.FromResult(new CommandLineResult(string.Empty, "apply error", ApplyExitCode));
        }

        public Task<CommandLineResult> RunDestroyAsync(string executeDirectory, string planFile)
        {
            DestroyPlanFile = planFile;
            return Task.FromResult(new CommandLineResult(string.Empty, "destroy error", ApplyExitCode));
        }
    }
}
