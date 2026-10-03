using NSubstitute;
using Orchitect.Engine.Contracts.Runner.Api;
using Orchitect.Engine.Execution.Provisioner;
using Orchitect.Engine.Execution.Provisioner.Terraform;
using Orchitect.Engine.Execution.Provisioner.Terraform.Models;

namespace Orchitect.Engine.Execution.Unit.Tests.Terraform;

public sealed class TerraformProvisionerTests
{
    private readonly ITerraformDriver _driver = Substitute.For<ITerraformDriver>();
    private readonly TerraformPlanResult _planResult = new("/work", "/work/plan.tfplan", TerraformPlanResultState.Success);

    public TerraformProvisionerTests()
    {
        _driver.PlanAsync(Arg.Any<List<RunInput>>(), Arg.Any<RunContext>(), Arg.Any<bool>(),
            Arg.Any<CancellationToken>()).Returns(_planResult);
    }

    [Fact]
    public void Provider_IsTerraform()
    {
        Assert.Equal(RunInputProvider.Terraform, new TerraformProvisioner(_driver).Provider);
    }

    [Fact]
    public async Task ProvisionAsync_TerraformInputs_PlansThenApplies()
    {
        var context = TerraformTestData.NewContext();
        var terraformInput = Input(RunInputProvider.Terraform, "storage");

        await new TerraformProvisioner(_driver).ProvisionAsync(
            [terraformInput, Input(RunInputProvider.Helm, "chart")], context);

        await _driver.Received(1).PlanAsync(
            Arg.Is<List<RunInput>>(inputs => IsOnly(inputs, terraformInput)), context, false,
            Arg.Any<CancellationToken>());
        await _driver.Received(1).ApplyAsync(_planResult, Arg.Any<CancellationToken>());
        await _driver.DidNotReceiveWithAnyArgs().DestroyAsync(default!);
    }

    [Fact]
    public async Task ProvisionAsync_NoTerraformInputs_DoesNothing()
    {
        await new TerraformProvisioner(_driver).ProvisionAsync(
            [Input(RunInputProvider.Helm, "chart")], TerraformTestData.NewContext());

        Assert.Empty(_driver.ReceivedCalls());
    }

    [Fact]
    public async Task DeleteAsync_TerraformInputs_PlansDestroyThenDestroys()
    {
        var context = TerraformTestData.NewContext();
        var terraformInput = Input(RunInputProvider.Terraform, "storage");

        await new TerraformProvisioner(_driver).DeleteAsync(
            [terraformInput, Input(RunInputProvider.Helm, "chart")], context);

        await _driver.Received(1).PlanAsync(
            Arg.Is<List<RunInput>>(inputs => IsOnly(inputs, terraformInput)), context, true,
            Arg.Any<CancellationToken>());
        await _driver.Received(1).DestroyAsync(_planResult, Arg.Any<CancellationToken>());
        await _driver.DidNotReceiveWithAnyArgs().ApplyAsync(default!);
    }

    [Fact]
    public async Task DeleteAsync_NoTerraformInputs_DoesNothing()
    {
        await new TerraformProvisioner(_driver).DeleteAsync(
            [Input(RunInputProvider.Helm, "chart")], TerraformTestData.NewContext());

        Assert.Empty(_driver.ReceivedCalls());
    }

    private static RunInput Input(RunInputProvider provider, string key) =>
        TerraformTestData.Input(provider, key, new Dictionary<string, string> { ["name"] = key });

    private static bool IsOnly(List<RunInput> inputs, RunInput expected) =>
        inputs.Count == 1 && inputs[0] == expected;
}
