using NSubstitute;
using Orchitect.Domain.Engine.ResourceTemplate;
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
        _driver.PlanAsync(Arg.Any<List<TerraformPlanInput>>(), Arg.Any<ProvisionContext>(), Arg.Any<bool>(),
            Arg.Any<CancellationToken>()).Returns(_planResult);
    }

    [Fact]
    public void Provider_IsTerraform()
    {
        Assert.Equal(ResourceTemplateProvider.Terraform, new TerraformProvisioner(_driver).Provider);
    }

    [Fact]
    public async Task ProvisionAsync_TerraformInputs_PlansThenApplies()
    {
        var context = TerraformTestData.NewContext();
        var terraformInput = Input(ResourceTemplateProvider.Terraform, "storage");

        await new TerraformProvisioner(_driver).ProvisionAsync(
            [terraformInput, Input(ResourceTemplateProvider.Helm, "chart")], context);

        await _driver.Received(1).PlanAsync(
            Arg.Is<List<TerraformPlanInput>>(inputs => IsOnly(inputs, terraformInput)), context, false,
            Arg.Any<CancellationToken>());
        await _driver.Received(1).ApplyAsync(_planResult, Arg.Any<CancellationToken>());
        await _driver.DidNotReceiveWithAnyArgs().DestroyAsync(default!);
    }

    [Fact]
    public async Task ProvisionAsync_NoTerraformInputs_DoesNothing()
    {
        await new TerraformProvisioner(_driver).ProvisionAsync(
            [Input(ResourceTemplateProvider.Helm, "chart")], TerraformTestData.NewContext());

        Assert.Empty(_driver.ReceivedCalls());
    }

    [Fact]
    public async Task DeleteAsync_TerraformInputs_PlansDestroyThenDestroys()
    {
        var context = TerraformTestData.NewContext();
        var terraformInput = Input(ResourceTemplateProvider.Terraform, "storage");

        await new TerraformProvisioner(_driver).DeleteAsync(
            [terraformInput, Input(ResourceTemplateProvider.Helm, "chart")], context);

        await _driver.Received(1).PlanAsync(
            Arg.Is<List<TerraformPlanInput>>(inputs => IsOnly(inputs, terraformInput)), context, true,
            Arg.Any<CancellationToken>());
        await _driver.Received(1).DestroyAsync(_planResult, Arg.Any<CancellationToken>());
        await _driver.DidNotReceiveWithAnyArgs().ApplyAsync(default!);
    }

    [Fact]
    public async Task DeleteAsync_NoTerraformInputs_DoesNothing()
    {
        await new TerraformProvisioner(_driver).DeleteAsync(
            [Input(ResourceTemplateProvider.Helm, "chart")], TerraformTestData.NewContext());

        Assert.Empty(_driver.ReceivedCalls());
    }

    private static ProvisionInput Input(ResourceTemplateProvider provider, string key) =>
        new(TerraformTestData.Template(provider), new Dictionary<string, string> { ["name"] = key }, key);

    private static bool IsOnly(List<TerraformPlanInput> inputs, ProvisionInput expected) =>
        inputs.Count == 1 &&
        inputs[0].Template == expected.Template &&
        inputs[0].Inputs == expected.Inputs &&
        inputs[0].Key == expected.Key;
}
