using System.Globalization;
using System.Net.Http.Headers;
using Azure.Security.KeyVault.Secrets;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Options;
using Orchitect.Engine.Contracts.Runner;
using Orchitect.Engine.Contracts.Runner.Api;
using Orchitect.Engine.Contracts.Secret;
using Orchitect.Engine.Contracts.Terraform;
using Orchitect.Engine.Execution.Configuration.Score;
using Orchitect.Engine.Execution.Provisioner;
using Orchitect.Engine.Execution.Provisioner.Helm;
using Orchitect.Engine.Execution.Provisioner.Terraform;
using Orchitect.Engine.Execution.RunnerApi;
using Orchitect.Engine.Execution.Secret;
using Orchitect.Engine.Execution.Secret.Azure;
using Orchitect.Engine.Execution.Shared.CommandLine;
using Polly;

namespace Orchitect.Engine.Execution;

public static class ExecutionExtensions
{
    public static IServiceCollection AddEngineProvisioningServices(this IServiceCollection services)
    {
        services.AddSharedServices();
        services.AddScoreServices();
        services.AddHelmServices();
        services.AddTerraformServices();
        return services;
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

    public static IServiceCollection AddRunnerApiClient(this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOptions<RunnerApiOptions>()
            .Configure(options =>
            {
                options.BaseUrl = Uri.TryCreate(configuration[RunnerEnvironment.ApiBaseUrl], UriKind.Absolute,
                    out var baseUrl)
                    ? baseUrl
                    : null;
                options.RunId = Guid.TryParse(configuration[RunnerEnvironment.RunId], out var runId)
                    ? runId
                    : Guid.Empty;
                options.Token = configuration[RunnerEnvironment.RunToken] ?? string.Empty;
            })
            .ValidateOnStart();
        services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IValidateOptions<RunnerApiOptions>, RunnerApiOptionsValidator>());
        services.TryAddTransient<IRunCompletionReporter, RunCompletionReporter>();

        var httpClient = services.AddHttpClient<IRunnerApiClient, RunnerApiClient>((provider, client) =>
            {
                var options = provider.GetRequiredService<IOptions<RunnerApiOptions>>().Value;
                var baseUrl = options.BaseUrl!.AbsoluteUri;

                client.BaseAddress = new Uri(baseUrl.EndsWith('/') ? baseUrl : $"{baseUrl}/");
                client.Timeout = Timeout.InfiniteTimeSpan;
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", options.Token);
                client.DefaultRequestHeaders.Add(RunnerContract.HeaderName,
                    RunnerContract.Version.ToString(CultureInfo.InvariantCulture));
            });

#pragma warning disable EXTEXP0001
        httpClient.RemoveAllResilienceHandlers();
#pragma warning restore EXTEXP0001

        httpClient.AddResilienceHandler("runner-api", (pipeline, context) =>
        {
            var options = context.ServiceProvider.GetRequiredService<IOptions<RunnerApiOptions>>().Value;

            pipeline.AddTimeout(options.TotalTimeout);
            pipeline.AddRetry(new HttpRetryStrategyOptions
            {
                MaxRetryAttempts = options.MaxRetryAttempts,
                Delay = options.RetryDelay,
                BackoffType = DelayBackoffType.Exponential,
                UseJitter = true
            });
            pipeline.AddTimeout(options.AttemptTimeout);
        });

        return services;
    }
}
