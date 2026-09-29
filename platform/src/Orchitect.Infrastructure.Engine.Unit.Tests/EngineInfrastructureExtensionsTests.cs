using Docker.DotNet;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Orchitect.Infrastructure.Engine.Executor;
using Orchitect.Infrastructure.Engine.Queue;

namespace Orchitect.Infrastructure.Engine.Unit.Tests;

public sealed class EngineInfrastructureExtensionsTests
{
    private static readonly Type[] ExecutionServices =
    [
        typeof(IExecutor),
        typeof(IDockerClient),
        typeof(IDeploymentQueue),
        typeof(IBackgroundTaskQueueProcessor),
        typeof(IHostedService),
        typeof(IConfigureOptions<ExecutorOptions>)
    ];

    [Fact]
    public void AddEngineProvisioningServices_DoesNotRegisterExecutionServices()
    {
        var services = new ServiceCollection().AddEngineProvisioningServices();

        Assert.Contains(services, d => d.ServiceType == typeof(IEngineOrchestrator));
        Assert.All(ExecutionServices, type => Assert.DoesNotContain(services, d => d.ServiceType == type));
    }

    [Fact]
    public void AddEngineExecutionServices_RegistersExecutionServices()
    {
        var services = new ServiceCollection()
            .AddEngineExecutionServices(new ConfigurationBuilder().Build());

        Assert.All(ExecutionServices, type => Assert.Contains(services, d => d.ServiceType == type));
        Assert.DoesNotContain(services, d => d.ServiceType == typeof(IEngineOrchestrator));
    }
}
