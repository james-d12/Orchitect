using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Orchitect.Engine.Contracts.Runner.Api;
using Orchitect.Engine.Execution.Artifact;
using Orchitect.Engine.Execution.Provisioner.Terraform;
using Orchitect.Engine.Execution.Provisioner.Terraform.Models;
using Orchitect.Engine.Execution.Shared.CommandLine;

namespace Orchitect.Engine.Execution.Unit.Tests.Terraform;

public sealed class TerraformDriverTests
{
    [Fact]
    public async Task PlanAsync_ProvisionAndDestroy_InitAgainstTheSameBackendState()
    {
        var context = TerraformTestData.NewContext();
        var commandLine = new RecordingTerraformCommandLine();
        var driver = CreateDriver(commandLine);
        List<RunInput> inputs = [TerraformTestData.PlanInput()];

        await driver.PlanAsync(inputs, context);
        await driver.PlanAsync(inputs, context, destroy: true);

        Assert.Equal(2, commandLine.InitBackendConfigs.Count);
        Assert.Equal(commandLine.InitBackendConfigs[0], commandLine.InitBackendConfigs[1]);
        Assert.Contains($"key = \"{context.ApplicationId}/{context.EnvironmentId}.tfstate\"",
            commandLine.InitBackendConfigs[0]);
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
        Assert.NotNull(commandLine.ApplyPlanFile);
        Assert.Equal(commandLine.PlanDestroyOutput, commandLine.ApplyPlanFile);
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

    [Theory]
    [InlineData(137)]
    [InlineData(143)]
    [InlineData(-1)]
    public async Task PlanAsync_UnexpectedExitCode_IsPlanFailed(int exitCode)
    {
        var commandLine = new RecordingTerraformCommandLine { PlanExitCode = exitCode };
        var driver = CreateDriver(commandLine);

        var planResult = await driver.PlanAsync([TerraformTestData.PlanInput()], TerraformTestData.NewContext());

        Assert.Equal(TerraformPlanResultState.PlanFailed, planResult.State);
        await Assert.ThrowsAsync<InvalidOperationException>(() => driver.ApplyAsync(planResult));
        Assert.False(commandLine.ApplyCalled);
    }

    [Fact]
    public async Task PlanAsync_ChangesNeeded_IsSuccess()
    {
        var driver = CreateDriver(new RecordingTerraformCommandLine());

        var planResult = await driver.PlanAsync([TerraformTestData.PlanInput()], TerraformTestData.NewContext());

        Assert.Equal(TerraformPlanResultState.Success, planResult.State);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PlanAndApply_PassCancellationTokenToEveryTerraformCommand(bool destroy)
    {
        using var cancellation = new CancellationTokenSource();
        var commandLine = new RecordingTerraformCommandLine();
        var driver = CreateDriver(commandLine);

        var planResult = await driver.PlanAsync([TerraformTestData.PlanInput()], TerraformTestData.NewContext(),
            destroy, cancellation.Token);
        await (destroy
            ? driver.DestroyAsync(planResult, cancellation.Token)
            : driver.ApplyAsync(planResult, cancellation.Token));

        Assert.Equal(4, commandLine.Tokens.Count);
        Assert.All(commandLine.Tokens, token => Assert.Equal(cancellation.Token, token));
    }

    [Fact]
    public async Task PlanAsync_ValidationFails_MessageContainsReasons()
    {
        var validator = new FixedTerraformValidator(
            TerraformValidationResult.InputInvalid("These inputs were not present in the terraform module: sku"));
        var driver = CreateDriver(new RecordingTerraformCommandLine(), validator);

        var planResult = await driver.PlanAsync([TerraformTestData.PlanInput()], TerraformTestData.NewContext());

        Assert.Equal(TerraformPlanResultState.PreValidationFailed, planResult.State);
        Assert.Contains("Storage Account", planResult.Message);
        Assert.Contains("sku", planResult.Message);
    }

    [Fact]
    public async Task ApplyAsync_ArtifactsEnabled_SavesEachCommandLogAndThePlan()
    {
        var commandLine = new RecordingTerraformCommandLine();
        var artifacts = new RecordingRunArtifactStore();
        var driver = CreateDriver(commandLine, artifactStore: artifacts);

        var planResult = await driver.PlanAsync([TerraformTestData.PlanInput()], TerraformTestData.NewContext());
        await driver.ApplyAsync(planResult);

        Assert.Equal(["terraform-init.log", "terraform-validate.log", "terraform-plan.log", "terraform-apply.log"],
            artifacts.Texts.Where(t => t.Kind == RunArtifactKind.Log).Select(t => t.Name));
        Assert.Contains("apply error", artifacts.Texts.Single(t => t.Name == "terraform-apply.log").Content);

        var planName = Path.GetFileName(planResult.PlanFilePath);
        Assert.Equal(planResult.PlanFilePath, commandLine.ShowPlanFile);
        Assert.Equal([(RunArtifactKind.Plan, planName, planResult.PlanFilePath)], artifacts.Files);
        var planJson = artifacts.Texts.Single(t => t.Kind == RunArtifactKind.Plan);
        Assert.Equal(Path.ChangeExtension(planName, ".json"), planJson.Name);
        Assert.Equal(commandLine.PlanJson, planJson.Content);
    }

    [Fact]
    public async Task PlanAsync_ArtifactsDisabled_DoesNotRenderThePlan()
    {
        var commandLine = new RecordingTerraformCommandLine();
        var driver = CreateDriver(commandLine);

        await driver.PlanAsync([TerraformTestData.PlanInput()], TerraformTestData.NewContext());

        Assert.Null(commandLine.ShowPlanFile);
    }

    [Fact]
    public async Task PlanAsync_ShowFails_KeepsThePlanWithoutItsJson()
    {
        var artifacts = new RecordingRunArtifactStore();
        var driver = CreateDriver(new RecordingTerraformCommandLine { ShowExitCode = 1 }, artifactStore: artifacts);

        var planResult = await driver.PlanAsync([TerraformTestData.PlanInput()], TerraformTestData.NewContext());

        Assert.Equal(TerraformPlanResultState.Success, planResult.State);
        Assert.Single(artifacts.Files);
        Assert.DoesNotContain(artifacts.Texts, t => t.Kind == RunArtifactKind.Plan);
    }

    [Fact]
    public async Task ApplyAsync_ApplyExitsNonZero_SavesTheApplyLogBeforeThrowing()
    {
        var artifacts = new RecordingRunArtifactStore();
        var driver = CreateDriver(new RecordingTerraformCommandLine { ApplyExitCode = 1 }, artifactStore: artifacts);
        var planResult = await driver.PlanAsync([TerraformTestData.PlanInput()], TerraformTestData.NewContext());

        await Assert.ThrowsAsync<InvalidOperationException>(() => driver.ApplyAsync(planResult));

        Assert.StartsWith("exit code: 1", artifacts.Texts.Single(t => t.Name == "terraform-apply.log").Content);
    }

    private static TerraformDriver CreateDriver(ITerraformCommandLine commandLine,
        ITerraformValidator? validator = null, IRunArtifactStore? artifactStore = null)
    {
        var projectBuilder = new TerraformProjectBuilder(NullLogger<TerraformProjectBuilder>.Instance,
            new TerraformRenderer(), Options.Create(TerraformTestData.AzureBackend()));

        return new TerraformDriver(NullLogger<TerraformDriver>.Instance,
            validator ?? new FixedTerraformValidator(TerraformTestData.ValidResult()), commandLine, projectBuilder,
            artifactStore ?? NullRunArtifactStore.Instance);
    }

    private sealed class FixedTerraformValidator(TerraformValidationResult result) : ITerraformValidator
    {
        public Task<Dictionary<RunInput, TerraformValidationResult>> ValidateAsync(
            List<RunInput> terraformPlanInputs, CancellationToken cancellationToken) =>
            Task.FromResult(terraformPlanInputs.ToDictionary(input => input, _ => result));
    }

    private sealed class RecordingTerraformCommandLine : ITerraformCommandLine
    {
        private static readonly CommandLineResult Success = new(string.Empty, string.Empty, 0);

        public int InitExitCode { get; init; }
        public int PlanExitCode { get; init; } = (int)TerraformPlanResultExitCode.ChangesNeeded;
        public int ApplyExitCode { get; init; }
        public int ShowExitCode { get; init; }
        public string PlanJson { get; init; } = "{\"format_version\":\"1.2\"}";
        public string? ShowPlanFile { get; private set; }

        public List<string?> InitBackendConfigs { get; } = [];
        public string? PlanDestroyOutput { get; private set; }
        public string? ApplyPlanFile { get; private set; }
        public bool ApplyCalled { get; private set; }
        public List<CancellationToken> Tokens { get; } = [];

        public Task<CommandLineResult> RunTerraformJsonOutput(string executeDirectory,
            CancellationToken cancellationToken) => Task.FromResult(Success);

        public Task<CommandLineResult> RunInitAsync(string executeDirectory,
            string? backendConfigFile, CancellationToken cancellationToken)
        {
            InitBackendConfigs.Add(backendConfigFile is null ? null : File.ReadAllText(backendConfigFile));
            Tokens.Add(cancellationToken);
            return Task.FromResult(new CommandLineResult(string.Empty, "init error", InitExitCode));
        }

        public Task<CommandLineResult> RunValidateAsync(string executeDirectory, CancellationToken cancellationToken)
        {
            Tokens.Add(cancellationToken);
            return Task.FromResult(Success);
        }

        public Task<CommandLineResult> RunPlanDestroyAsync(string executeDirectory, string planFileOutput,
            CancellationToken cancellationToken)
        {
            PlanDestroyOutput = planFileOutput;
            Tokens.Add(cancellationToken);
            return Task.FromResult(new CommandLineResult(string.Empty, "plan error", PlanExitCode));
        }

        public Task<CommandLineResult> RunPlanAsync(string executeDirectory, string planFileOutput,
            CancellationToken cancellationToken)
        {
            Tokens.Add(cancellationToken);
            return Task.FromResult(new CommandLineResult(string.Empty, "plan error", PlanExitCode));
        }

        public Task<CommandLineResult> RunApplyAsync(string executeDirectory, string planFile,
            CancellationToken cancellationToken)
        {
            ApplyCalled = true;
            ApplyPlanFile = planFile;
            Tokens.Add(cancellationToken);
            return Task.FromResult(new CommandLineResult(string.Empty, "apply error", ApplyExitCode));
        }

        public Task<CommandLineResult> RunShowJsonAsync(string executeDirectory, string planFile,
            CancellationToken cancellationToken)
        {
            ShowPlanFile = planFile;
            Tokens.Add(cancellationToken);
            return Task.FromResult(new CommandLineResult(PlanJson, "show error", ShowExitCode));
        }
    }

    private sealed class RecordingRunArtifactStore : IRunArtifactStore
    {
        public List<(RunArtifactKind Kind, string Name, string Content)> Texts { get; } = [];
        public List<(RunArtifactKind Kind, string Name, string Path)> Files { get; } = [];

        public bool IsEnabled => true;

        public Task SaveTextAsync(RunArtifactKind kind, string name, string content,
            CancellationToken cancellationToken = default)
        {
            Texts.Add((kind, name, content));
            return Task.CompletedTask;
        }

        public Task SaveFileAsync(RunArtifactKind kind, string name, string path,
            CancellationToken cancellationToken = default)
        {
            Files.Add((kind, name, path));
            return Task.CompletedTask;
        }
    }
}
