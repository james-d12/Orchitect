using Azure.Identity;
using Docker.DotNet;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Orchitect.Engine.Dispatch.Completion;
using Orchitect.Engine.Dispatch.Executor;
using Orchitect.Engine.Dispatch.Plan;
using Orchitect.Engine.Dispatch.Queue;
using Orchitect.Engine.Dispatch.Secret;
using Orchitect.Engine.Dispatch.Secret.Azure;

namespace Orchitect.Engine.Dispatch;

public static class DispatchExtensions
{
    public static IServiceCollection AddEngineDispatchServices(this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddExecutorServices(configuration);
        services.AddQueueServices();
        services.AddRunServices();
        return services;
    }

    public static IServiceCollection AddRunServices(this IServiceCollection services)
    {
        services.TryAddScoped<IRunPlanner, RunPlanner>();
        services.TryAddScoped<IRunCompleter, RunCompleter>();
        return services;
    }

    private static void AddExecutorServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<ExecutorOptions>()
            .Bind(configuration.GetSection(ExecutorOptions.SectionName))
            .ValidateDataAnnotations()
            .Validate(options => !string.IsNullOrWhiteSpace(options.Image), "ExecutorOptions:Image is required.")
            .Validate(options => options.StopGracePeriod > TimeSpan.Zero,
                "ExecutorOptions:StopGracePeriod must be greater than zero.")
            .Validate(options => options.Timeout > TimeSpan.Zero,
                "ExecutorOptions:Timeout must be greater than zero.")
            .Validate(options => options.TerraformBackend.GetValidationError() is null,
                "ExecutorOptions:TerraformBackend is invalid. Mode Remote needs Type and Config; Mode Local must not set them.")
            .Validate(options => options.SecretProvider.GetValidationError() is null,
                "ExecutorOptions:SecretProvider is invalid. AzureKeyVault needs an absolute AzureKeyVault:VaultUri.")
            .ValidateOnStart();

        services.TryAddSingleton<IDockerClient>(_ => new DockerClientBuilder().Build());
        services.TryAddSingleton<IExecutor, DockerExecutor>();
        services.AddHostedService<RunnerContainerSweepService>();
        services.TryAddSingleton<IRunnerSecretTokenProvider>(_ =>
            new KeyVaultRunnerTokenProvider(new DefaultAzureCredential()));
    }

    private static void AddQueueServices(this IServiceCollection services)
    {
        services.AddHostedService<QueuedHostedService>();
        services.AddSingleton<IBackgroundTaskQueueProcessor>(_ => new BackgroundTaskQueueProcessor(5));
        services.AddSingleton<IDeploymentRunCancellation, DeploymentRunCancellation>();
        services.AddScoped<IDeploymentQueue, DeploymentQueue>();
    }
}
