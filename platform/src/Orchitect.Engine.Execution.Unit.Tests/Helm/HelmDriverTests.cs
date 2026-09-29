using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Orchitect.Domain.Engine.ResourceTemplate;
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
        var template = TerraformTestData.Template(ResourceTemplateProvider.Helm);
        Dictionary<string, string> inputs = new() { ["replicaCount"] = "2" };
        _validator.ValidateAsync(template, inputs).Returns(Result(state));

        await CreateDriver().PlanAsync(template, inputs);

        await _validator.Received(1).ValidateAsync(template, inputs);
    }

    [Fact]
    public async Task PlanAsync_UndefinedState_Throws()
    {
        _validator.ValidateAsync(Arg.Any<ResourceTemplate>(), Arg.Any<Dictionary<string, string>>())
            .Returns(Result((HelmValidationResultState)99));

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            CreateDriver().PlanAsync(TerraformTestData.Template(ResourceTemplateProvider.Helm), []));
    }

    private HelmDriver CreateDriver() => new(NullLogger<HelmDriver>.Instance, _validator);

    private static HelmValidationResult Result(HelmValidationResultState state) =>
        new() { State = state, Config = null };
}
