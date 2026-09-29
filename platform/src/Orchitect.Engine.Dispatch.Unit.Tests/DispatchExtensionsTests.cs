using Docker.DotNet;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Orchitect.Engine.Dispatch.Executor;
using Orchitect.Engine.Dispatch.Queue;
using Orchitect.Engine.Dispatch.Secret;

namespace Orchitect.Engine.Dispatch.Unit.Tests;

public sealed class DispatchExtensionsTests
{
    private static readonly Type[] DispatchServices =
    [
        typeof(IExecutor),
        typeof(IDockerClient),
        typeof(IDeploymentQueue),
        typeof(IBackgroundTaskQueueProcessor),
        typeof(IRunnerSecretTokenProvider),
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
}
