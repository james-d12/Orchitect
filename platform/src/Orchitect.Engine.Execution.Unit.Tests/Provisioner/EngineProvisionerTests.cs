using Orchitect.Domain.Engine.ResourceTemplate;
using Orchitect.Engine.Execution.Provisioner;
using Orchitect.Engine.Execution.Unit.Tests.Terraform;

namespace Orchitect.Engine.Execution.Unit.Tests.Provisioner;

public sealed class EngineProvisionerTests
{
    private readonly RecordingProvisioner _terraform = new(ResourceTemplateProvider.Terraform);
    private readonly RecordingProvisioner _helm = new(ResourceTemplateProvider.Helm);

    [Fact]
    public async Task ProvisionAsync_MixedProviders_RoutesEachGroupToItsProvisioner()
    {
        var context = TerraformTestData.NewContext();
        var storage = Input(ResourceTemplateProvider.Terraform, "storage");
        var network = Input(ResourceTemplateProvider.Terraform, "network");
        var chart = Input(ResourceTemplateProvider.Helm, "chart");

        await new EngineProvisioner([_terraform, _helm]).ProvisionAsync([storage, chart, network], context);

        Assert.Equal([storage, network], _terraform.Provisioned);
        Assert.Equal([chart], _helm.Provisioned);
        Assert.Same(context, _terraform.Context);
        Assert.Null(_terraform.Deleted);
        Assert.Null(_helm.Deleted);
    }

    [Fact]
    public async Task DeleteAsync_MixedProviders_RoutesEachGroupToItsProvisioner()
    {
        var context = TerraformTestData.NewContext();
        var storage = Input(ResourceTemplateProvider.Terraform, "storage");
        var chart = Input(ResourceTemplateProvider.Helm, "chart");

        await new EngineProvisioner([_terraform, _helm]).DeleteAsync([storage, chart], context);

        Assert.Equal([storage], _terraform.Deleted);
        Assert.Equal([chart], _helm.Deleted);
        Assert.Same(context, _helm.Context);
        Assert.Null(_terraform.Provisioned);
        Assert.Null(_helm.Provisioned);
    }

    [Fact]
    public async Task ProvisionAsync_NoProvisionerForProvider_Throws()
    {
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new EngineProvisioner([_terraform]).ProvisionAsync(
                [Input(ResourceTemplateProvider.Helm, "chart")], TerraformTestData.NewContext()));

        Assert.Contains(nameof(ResourceTemplateProvider.Helm), exception.Message);
    }

    [Fact]
    public async Task DeleteAsync_NoProvisionerForProvider_Throws()
    {
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new EngineProvisioner([_terraform]).DeleteAsync(
                [Input(ResourceTemplateProvider.Helm, "chart")], TerraformTestData.NewContext()));

        Assert.Contains(nameof(ResourceTemplateProvider.Helm), exception.Message);
    }

    private static ProvisionInput Input(ResourceTemplateProvider provider, string key) =>
        new(TerraformTestData.Template(provider), [], key);

    private sealed class RecordingProvisioner(ResourceTemplateProvider provider) : IProvisioner
    {
        public ResourceTemplateProvider Provider => provider;
        public List<ProvisionInput>? Provisioned { get; private set; }
        public List<ProvisionInput>? Deleted { get; private set; }
        public ProvisionContext? Context { get; private set; }

        public Task ProvisionAsync(List<ProvisionInput> inputs, ProvisionContext context,
            CancellationToken cancellationToken = default)
        {
            Provisioned = inputs;
            Context = context;
            return Task.CompletedTask;
        }

        public Task DeleteAsync(List<ProvisionInput> inputs, ProvisionContext context,
            CancellationToken cancellationToken = default)
        {
            Deleted = inputs;
            Context = context;
            return Task.CompletedTask;
        }
    }
}
