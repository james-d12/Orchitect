using Docker.DotNet;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Orchitect.Engine.Dispatch.Completion;
using Orchitect.Engine.Dispatch.Executor;
using Orchitect.Engine.Dispatch.Queue;
using Orchitect.Engine.Dispatch.Secret;
using Orchitect.Engine.Dispatch.Storage;

namespace Orchitect.Engine.Dispatch.Unit.Tests;

public sealed class DispatchExtensionsTests
{
    private static readonly Type[] DispatchServices =
    [
        typeof(IExecutor),
        typeof(IDockerClient),
        typeof(IDeploymentQueue),
        typeof(IRunCompletionHandler),
        typeof(IBackgroundTaskQueueProcessor),
        typeof(IRunnerSecretTokenProvider),
        typeof(IRunnerStorageTokenProvider),
        typeof(IHostedService),
        typeof(IConfigureOptions<ExecutorOptions>)
    ];

    [Fact]
    public void AddEngineDispatchServices_RegistersDispatchServices()
    {
        var services = new ServiceCollection()
            .AddEngineDispatchServices(new ConfigurationBuilder().Build());

        Assert.All(DispatchServices, type => Assert.Contains(services, d => d.ServiceType == type));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("/relative")]
    [InlineData("ftp://localhost:41005")]
    public void ExecutorOptions_InvalidApiBaseUrl_FailsValidation(string? apiBaseUrl)
    {
        var exception = Assert.Throws<OptionsValidationException>(() => ResolveExecutorOptions(apiBaseUrl));

        Assert.Contains(exception.Failures, failure => failure.Contains("ExecutorOptions:ApiBaseUrl"));
    }

    [Fact]
    public void ExecutorOptions_AbsoluteApiBaseUrl_PassesValidation()
    {
        var options = ResolveExecutorOptions("http://localhost:41005");

        Assert.Equal(new Uri("http://localhost:41005"), options.ApiBaseUrl);
    }

    [Theory]
    [InlineData("FileSystem", null, null, "Storage")]
    [InlineData("FileSystem", "relative/artifacts", null, "Storage")]
    [InlineData("AzureBlob", null, null, "Storage")]
    [InlineData("None", null, "/srv/artifacts", "StorageHostPath")]
    [InlineData("FileSystem", "/var/orchitect/artifacts", "relative", "StorageHostPath")]
    public void ExecutorOptions_InvalidStorage_FailsValidation(string type, string? rootPath, string? hostPath,
        string expected)
    {
        var exception = Assert.Throws<OptionsValidationException>(() => ResolveExecutorOptions(
            "http://localhost:41005", new()
            {
                ["ExecutorOptions:Storage:Type"] = type,
                ["ExecutorOptions:Storage:FileSystem:RootPath"] = rootPath,
                ["ExecutorOptions:StorageHostPath"] = hostPath
            }));

        Assert.Contains(exception.Failures, failure => failure.Contains($"ExecutorOptions:{expected}"));
    }

    [Fact]
    public void ExecutorOptions_FileSystemStorageWithHostPath_PassesValidation()
    {
        var options = ResolveExecutorOptions("http://localhost:41005", new()
        {
            ["ExecutorOptions:Storage:Type"] = "FileSystem",
            ["ExecutorOptions:Storage:FileSystem:RootPath"] = "/var/orchitect/artifacts",
            ["ExecutorOptions:StorageHostPath"] = "/srv/orchitect/artifacts"
        });

        Assert.Equal("/srv/orchitect/artifacts", options.StorageHostPath);
    }

    private static ExecutorOptions ResolveExecutorOptions(string? apiBaseUrl,
        Dictionary<string, string?>? settings = null)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ExecutorOptions:Image"] = "orchitect-runner:test",
                ["ExecutorOptions:ApiBaseUrl"] = apiBaseUrl
            })
            .AddInMemoryCollection(settings ?? [])
            .Build();

        using var provider = new ServiceCollection()
            .AddEngineDispatchServices(configuration)
            .BuildServiceProvider();

        return provider.GetRequiredService<IOptions<ExecutorOptions>>().Value;
    }
}
