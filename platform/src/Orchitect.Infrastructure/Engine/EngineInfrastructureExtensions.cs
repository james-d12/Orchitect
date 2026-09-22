using Docker.DotNet;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Orchitect.Infrastructure.Engine.Configuration.Score;
using Orchitect.Infrastructure.Engine.Provisioner.Helm;
using Orchitect.Infrastructure.Engine.Provisioner.Terraform;
using Orchitect.Infrastructure.Engine.Runner;
using Orchitect.Infrastructure.Engine.Shared;
using Orchitect.Infrastructure.Engine.Shared.CommandLine;

namespace Orchitect.Infrastructure.Engine;

public static class EngineInfrastructureExtensions
{
    internal static void AddEngineInfrastructureServices(this IServiceCollection services)
    {
        services.AddSharedServices();
        services.AddScoreServices();
        services.AddHelmServices();
        services.AddTerraformServices();
        services.AddRunnerServices();
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
        services.AddSingleton<IProvisioner, TerraformProvisioner>();
        services.TryAddSingleton<ITerraformDriver, TerraformDriver>();
        services.TryAddSingleton<ITerraformProjectBuilder, TerraformProjectBuilder>();
        services.TryAddSingleton<ITerraformRenderer, TerraformRenderer>();
        services.TryAddSingleton<ITerraformCommandLine, TerraformCommandLine>();
        services.TryAddSingleton<ITerraformValidator, TerraformValidator>();
    }

    private static void AddRunnerServices(this IServiceCollection services)
    {
        services.TryAddSingleton(_ => new DockerClientConfiguration().CreateClient());
        services.TryAddSingleton<IRunner, DockerRunner>();
    }
}
