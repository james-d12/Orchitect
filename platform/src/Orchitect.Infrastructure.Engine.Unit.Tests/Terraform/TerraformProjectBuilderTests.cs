using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Orchitect.Infrastructure.Engine.Provisioner.Terraform;
using Orchitect.Infrastructure.Engine.Provisioner.Terraform.Models;

namespace Orchitect.Infrastructure.Engine.Unit.Tests.Terraform;

public sealed class TerraformProjectBuilderTests
{
    private static TerraformProjectBuilder CreateBuilder(TerraformBackendOptions backendOptions) =>
        new(NullLogger<TerraformProjectBuilder>.Instance, new TerraformRenderer(), Options.Create(backendOptions));

    [Fact]
    public async Task BuildProjectAsync_WithBackend_WritesBackendConfigFileAndResolvesPlaceholders()
    {
        var context = TerraformTestData.NewContext();
        var builder = CreateBuilder(TerraformTestData.AzureBackend());

        var result = await builder.BuildProjectAsync(TerraformTestData.ValidatedPlans(), context);

        Assert.True(File.Exists(Path.Combine(result.WorkingDirectory, "backend.tf.json")));
        Assert.Equal(Path.Combine(result.WorkingDirectory, "backend.tfbackend"), result.BackendConfigFile);
        Assert.Equal(
            "container_name = \"tfstate\"\n" +
            $"key = \"{context.ApplicationId}/{context.EnvironmentId}.tfstate\"\n" +
            "storage_account_name = \"orchitectstate\"\n",
            await File.ReadAllTextAsync(result.BackendConfigFile!));
    }

    [Fact]
    public async Task BuildProjectAsync_WithBackend_BackendConfigFileIsOwnerOnly()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        var builder = CreateBuilder(TerraformTestData.AzureBackend());

        var result = await builder.BuildProjectAsync(TerraformTestData.ValidatedPlans(), TerraformTestData.NewContext());

        Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(result.BackendConfigFile!));
    }

    [Fact]
    public async Task BuildProjectAsync_WithoutBackend_UsesLocalStateAndKeepsExistingFiles()
    {
        var context = TerraformTestData.NewContext();
        var builder = CreateBuilder(new TerraformBackendOptions());

        var first = await builder.BuildProjectAsync(TerraformTestData.ValidatedPlans(), context);
        var localState = Path.Combine(first.WorkingDirectory, "terraform.tfstate");
        await File.WriteAllTextAsync(localState, "{}");

        var second = await builder.BuildProjectAsync(TerraformTestData.ValidatedPlans(), context);

        Assert.False(File.Exists(Path.Combine(second.WorkingDirectory, "backend.tf.json")));
        Assert.Null(second.BackendConfigFile);
        Assert.True(File.Exists(localState));
    }

    [Fact]
    public async Task BuildProjectAsync_WithBackend_RecreatesExistingWorkingDirectory()
    {
        var context = TerraformTestData.NewContext();
        var builder = CreateBuilder(TerraformTestData.AzureBackend());

        var first = await builder.BuildProjectAsync(TerraformTestData.ValidatedPlans(), context);
        var leftover = Path.Combine(first.WorkingDirectory, "leftover.txt");
        await File.WriteAllTextAsync(leftover, "stale");

        var second = await builder.BuildProjectAsync(TerraformTestData.ValidatedPlans(), context);

        Assert.False(File.Exists(leftover));
        Assert.True(File.Exists(Path.Combine(second.WorkingDirectory, "main.tf.json")));
        Assert.True(Directory.Exists(second.PlanDirectory));
    }

    [Fact]
    public async Task BuildProjectAsync_WritesModulesAndInputValuesToSeparateFiles()
    {
        var builder = CreateBuilder(TerraformTestData.AzureBackend());

        var result = await builder.BuildProjectAsync(TerraformTestData.ValidatedPlans(), TerraformTestData.NewContext());

        var tfVars = await File.ReadAllTextAsync(Path.Combine(result.WorkingDirectory, "terraform.tfvars.json"));
        var main = await File.ReadAllTextAsync(Path.Combine(result.WorkingDirectory, "main.tf.json"));
        Assert.Contains("\"orders\"", tfVars);
        Assert.DoesNotContain("\"orders\"", main);
        Assert.True(File.Exists(Path.Combine(result.WorkingDirectory, "providers.tf.json")));
    }

    [Fact]
    public async Task BuildProjectAsync_WithoutBackend_RemovesPreviouslyGeneratedConfiguration()
    {
        var context = TerraformTestData.NewContext();
        var builder = CreateBuilder(new TerraformBackendOptions());

        var first = await builder.BuildProjectAsync(TerraformTestData.ValidatedPlans(), context);
        var legacyMainTf = Path.Combine(first.WorkingDirectory, "main.tf");
        await File.WriteAllTextAsync(legacyMainTf, "module \"old\" {}");

        var second = await builder.BuildProjectAsync(TerraformTestData.ValidatedPlans(), context);

        Assert.False(File.Exists(legacyMainTf));
        Assert.True(File.Exists(Path.Combine(second.WorkingDirectory, "main.tf.json")));
    }

    public static TheoryData<TerraformBackendOptions> InvalidBackends => new()
    {
        new TerraformBackendOptions { Mode = TerraformBackendMode.Remote, Type = "s3" },
        new TerraformBackendOptions
        {
            Mode = TerraformBackendMode.Remote,
            Config = new Dictionary<string, string> { ["bucket"] = "state" }
        },
        new TerraformBackendOptions { Mode = TerraformBackendMode.Local, Type = "azurerm" },
        new TerraformBackendOptions
        {
            Mode = TerraformBackendMode.Local,
            Config = new Dictionary<string, string> { ["key"] = "state" }
        }
    };

    [Theory]
    [MemberData(nameof(InvalidBackends))]
    public async Task BuildProjectAsync_InvalidBackend_Throws(TerraformBackendOptions backendOptions)
    {
        var builder = CreateBuilder(backendOptions);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            builder.BuildProjectAsync(TerraformTestData.ValidatedPlans(), TerraformTestData.NewContext()));
    }
}
