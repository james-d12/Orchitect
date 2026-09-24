using Orchitect.Infrastructure.Engine.Provisioner.Terraform;

namespace Orchitect.Infrastructure.Engine.Unit.Tests.Terraform;

public sealed class TerraformRendererTests
{
    [Theory]
    [InlineData("azurerm")]
    [InlineData("s3")]
    [InlineData("gcs")]
    public void RenderBackendTf_RendersPartialBackendBlockForType(string backendType)
    {
        var renderer = new TerraformRenderer();

        var backendTf = renderer.RenderBackendTf(backendType);

        Assert.Equal($"terraform {{\n  backend \"{backendType}\" {{}}\n}}\n", backendTf.ReplaceLineEndings("\n"));
    }
}
