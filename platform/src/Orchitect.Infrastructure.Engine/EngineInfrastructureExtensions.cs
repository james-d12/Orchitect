using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using Docker.DotNet;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Orchitect.Infrastructure.Engine.Configuration.Score;
using Orchitect.Infrastructure.Engine.Executor;
using Orchitect.Infrastructure.Engine.Provisioner;
using Orchitect.Infrastructure.Engine.Provisioner.Helm;
using Orchitect.Infrastructure.Engine.Provisioner.Terraform;
using Orchitect.Infrastructure.Engine.Provisioner.Terraform.Models;
using Orchitect.Infrastructure.Engine.Queue;
using Orchitect.Infrastructure.Engine.Secret;
using Orchitect.Infrastructure.Engine.Secret.Azure;
using Orchitect.Infrastructure.Engine.Shared.CommandLine;

namespace Orchitect.Infrastructure.Engine;

public static class EngineInfrastructureExtensions
{
    public static void AddEngineInfrastructureServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSharedServices();
        services.AddScoreServices();
        services.AddHelmServices();
        services.AddTerraformServices();
        services.AddExecutorServices(configuration);
        services.AddQueueServices();
    }

    private static void AddSharedServices(this IServiceCollection services)
    {
        services.TryAddSingleton<IGitCommandLine, GitCommandLine>();
        services.TryAddSingleton<IEngineProvisioner, EngineProvisioner>();
        services.TryAddScoped<IEngineOrchestrator, EngineOrchestrator>();
    }

    private static void AddScoreServices(this IServiceCollection services)
    {
        services.TryAddSingleton<IScoreDriver, ScoreDriver>();
    }

    private static void AddHelmServices(this IServiceCollection services)
    {
        services.TryAddSingleton<IHelmDriver, HelmDriver>();
        services.TryAddSingleton<IHelmValidator, HelmValidator>();
        services.TryAddSingleton<IHelmParser, HelmParser>();
    }

    private static void AddTerraformServices(this IServiceCollection services)
    {
        services.AddOptions<TerraformBackendOptions>().BindConfiguration(TerraformBackendOptions.SectionName);
        services.AddSingleton<IProvisioner, TerraformProvisioner>();
        services.TryAddSingleton<ITerraformDriver, TerraformDriver>();
        services.TryAddSingleton<ITerraformProjectBuilder, TerraformProjectBuilder>();
        services.TryAddSingleton<ITerraformRenderer, TerraformRenderer>();
        services.TryAddSingleton<ITerraformCommandLine, TerraformCommandLine>();
        services.TryAddSingleton<ITerraformModuleDownloader, TerraformModuleDownloader>();
        services.TryAddSingleton<ITerraformValidator, TerraformValidator>();
    }

    private static void AddExecutorServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<ExecutorOptions>()
            .Bind(configuration.GetSection(ExecutorOptions.SectionName))
            .ValidateDataAnnotations()
            .Validate(options => options.TerraformBackend.GetValidationError() is null,
                "RunnerOptions:TerraformBackend is invalid. Mode Remote needs Type and Config; Mode Local must not set them.")
            .Validate(options => options.SecretProvider.GetValidationError() is null,
                "RunnerOptions:SecretProvider is invalid. AzureKeyVault needs an absolute AzureKeyVault:VaultUri.")
            .ValidateOnStart();

        services.TryAddSingleton(_ => new DockerClientConfiguration().CreateClient());
        services.TryAddSingleton<IExecutor, DockerExecutor>();
        services.TryAddSingleton<IRunnerSecretTokenProvider>(_ =>
            new KeyVaultRunnerTokenProvider(new DefaultAzureCredential()));
    }

    private static void AddQueueServices(this IServiceCollection services)
    {
        services.AddHostedService<QueuedHostedService>();
        services.AddSingleton<IBackgroundTaskQueueProcessor>(_ => new BackgroundTaskQueueProcessor(5));
        services.AddScoped<IDeploymentQueue, DeploymentQueue>();
    }

    public static IServiceCollection AddRunnerServices(this IServiceCollection services,
        IConfiguration configuration)
    {
        var section = configuration.GetSection(SecretProviderOptions.SectionName);

        var options = section.Get<SecretProviderOptions>() ?? new SecretProviderOptions();

        if (options.GetValidationError() is { } error)
        {
            throw new InvalidOperationException(error);
        }

        services.AddOptions<SecretProviderOptions>().Bind(section);
        services.TryAddSingleton<ISecretEnvironmentLoader, SecretEnvironmentLoader>();

        switch (options.Type)
        {
            case SecretProviderType.Environment:
                services.TryAddSingleton<ISecretProvider, EnvironmentSecretProvider>();
                break;
            case SecretProviderType.AzureKeyVault:
                var keyVaultOptions = options.AzureKeyVault;
                services.TryAddSingleton(_ =>
                    new SecretClient(keyVaultOptions.VaultUri!, AzureCredentialFactory.Create(keyVaultOptions)));
                services.TryAddSingleton<ISecretProvider, AzureKeyVaultSecretProvider>();
                break;
            default:
                throw new InvalidOperationException(
                    $"{SecretProviderOptions.SectionName}:Type '{options.Type}' is not supported.");
        }

        return services;
    }
}
