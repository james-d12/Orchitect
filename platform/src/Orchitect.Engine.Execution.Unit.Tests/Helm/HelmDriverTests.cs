using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Orchitect.Engine.Contracts.Runner.Api;
using Orchitect.Engine.Execution.Provisioner.Helm;
using Orchitect.Engine.Execution.Provisioner.Helm.Models;
using Orchitect.Engine.Execution.Unit.Tests.Terraform;

namespace Orchitect.Engine.Execution.Unit.Tests.Helm;

public sealed class HelmDriverTests
{
    private readonly IHelmValidator _validator = Substitute.For<IHelmValidator>();

    [Theory]
    [InlineData(HelmValidationResultState.WrongProvider)]
    [InlineData(HelmValidationResultState.TemplateNotFound)]
    [InlineData(HelmValidationResultState.ModuleNotFound)]
    [InlineData(HelmValidationResultState.ModuleNotParsable)]
    [InlineData(HelmValidationResultState.InputNotPresent)]
    [InlineData(HelmValidationResultState.Valid)]
    public async Task PlanAsync_KnownState_Completes(HelmValidationResultState state)
    {
        var input = TerraformTestData.Input(RunInputProvider.Helm, "chart",
            new Dictionary<string, string> { ["replicaCount"] = "2" });
        _validator.ValidateAsync(input).Returns(Result(state));

        await CreateDriver().PlanAsync(input);

        await _validator.Received(1).ValidateAsync(input);
    }

    [Fact]
    public async Task PlanAsync_UndefinedState_Throws()
    {
        _validator.ValidateAsync(Arg.Any<RunInput>()).Returns(Result((HelmValidationResultState)99));

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            CreateDriver().PlanAsync(TerraformTestData.Input(RunInputProvider.Helm, "chart")));
    }

    private HelmDriver CreateDriver() => new(NullLogger<HelmDriver>.Instance, _validator);

    private static HelmValidationResult Result(HelmValidationResultState state) =>
        new() { State = state, Config = null };
}
