using Orchitect.Engine.Contracts.Runner.Api;
using Orchitect.Engine.Execution.Provisioner;
using Orchitect.Engine.Execution.Unit.Tests.Terraform;

namespace Orchitect.Engine.Execution.Unit.Tests.Provisioner;

public sealed class EngineProvisionerTests
{
    private readonly RecordingProvisioner _terraform = new(RunInputProvider.Terraform);
    private readonly RecordingProvisioner _helm = new(RunInputProvider.Helm);

    [Fact]
    public async Task ProvisionAsync_MixedProviders_RoutesEachGroupToItsProvisioner()
    {
        var context = TerraformTestData.NewContext();
        var storage = Input(RunInputProvider.Terraform, "storage");
        var network = Input(RunInputProvider.Terraform, "network");
        var chart = Input(RunInputProvider.Helm, "chart");

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
        var storage = Input(RunInputProvider.Terraform, "storage");
        var chart = Input(RunInputProvider.Helm, "chart");

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
                [Input(RunInputProvider.Helm, "chart")], TerraformTestData.NewContext()));

        Assert.Contains(nameof(RunInputProvider.Helm), exception.Message);
    }

    [Fact]
    public async Task DeleteAsync_NoProvisionerForProvider_Throws()
    {
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new EngineProvisioner([_terraform]).DeleteAsync(
                [Input(RunInputProvider.Helm, "chart")], TerraformTestData.NewContext()));

        Assert.Contains(nameof(RunInputProvider.Helm), exception.Message);
    }

    private static RunInput Input(RunInputProvider provider, string key) =>
        TerraformTestData.Input(provider, key);

    private sealed class RecordingProvisioner(RunInputProvider provider) : IProvisioner
    {
        public RunInputProvider Provider => provider;
        public List<RunInput>? Provisioned { get; private set; }
        public List<RunInput>? Deleted { get; private set; }
        public RunContext? Context { get; private set; }

        public Task ProvisionAsync(List<RunInput> inputs, RunContext context,
            CancellationToken cancellationToken = default)
        {
            Provisioned = inputs;
            Context = context;
            return Task.CompletedTask;
        }

        public Task DeleteAsync(List<RunInput> inputs, RunContext context,
            CancellationToken cancellationToken = default)
        {
            Deleted = inputs;
            Context = context;
            return Task.CompletedTask;
        }
    }
}
