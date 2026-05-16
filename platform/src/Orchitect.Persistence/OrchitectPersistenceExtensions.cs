using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Npgsql;
using Orchitect.Domain.Core.Credential;
using Orchitect.Domain.Core.Organisation;
using Orchitect.Domain.Engine.Application;
using Orchitect.Domain.Engine.Deployment;
using Orchitect.Domain.Engine.Environment;
using Orchitect.Domain.Engine.Resource;
using Orchitect.Domain.Engine.ResourceDependency;
using Orchitect.Domain.Engine.ResourceInstance;
using Orchitect.Domain.Engine.ResourceTemplate;
using Orchitect.Domain.Inventory.Cloud.Services;
using Orchitect.Domain.Inventory.Discovery.Services;
using Orchitect.Domain.Inventory.Identity.Services;
using Orchitect.Domain.Inventory.Issue.Services;
using Orchitect.Domain.Inventory.Pipeline.Services;
using Orchitect.Domain.Inventory.SourceControl.Services;
using Orchitect.Persistence.Repositories.Core;
using Orchitect.Persistence.Repositories.Engine;
using Orchitect.Persistence.Repositories.Inventory;

namespace Orchitect.Persistence;

public static class OrchitectPersistenceExtensions
{
    public static IServiceCollection AddPersistenceServices(this IServiceCollection services)
    {
        var connectionString = System.Environment.GetEnvironmentVariable("ConnectionStrings__orchitect");
        ArgumentException.ThrowIfNullOrEmpty(connectionString);

        var dataSourceBuilder = new NpgsqlDataSourceBuilder(connectionString);
        dataSourceBuilder.EnableDynamicJson();
        var dataSource = dataSourceBuilder.Build();

        var loggerFactory = LoggerFactory.Create(builder => builder.SetMinimumLevel(LogLevel.Information));

        services.AddDbContext<OrchitectDbContext>(options =>
            options.UseNpgsql(dataSource, opt => opt.EnableRetryOnFailure())
                .UseLoggerFactory(loggerFactory)
                .EnableSensitiveDataLogging());

        services.TryAddScoped<IOrganisationRepository, OrganisationRepository>();
        services.TryAddScoped<ICredentialRepository, CredentialRepository>();

        services.TryAddScoped<IDiscoveryConfigurationRepository, DiscoveryConfigurationRepository>();
        services.TryAddScoped<IRepositoryRepository, RepositoryRepository>();
        services.TryAddScoped<IPipelineRepository, PipelineRepository>();
        services.TryAddScoped<IPullRequestRepository, PullRequestRepository>();
        services.TryAddScoped<IUserRepository, UserRepository>();
        services.TryAddScoped<ICloudResourceRepository, CloudResourceRepository>();
        services.TryAddScoped<ICloudSecretRepository, CloudSecretRepository>();
        services.TryAddScoped<IIssueRepository, IssueRepository>();
        services.TryAddScoped<ITeamRepository, TeamRepository>();

        services.TryAddScoped<IResourceTemplateRepository, ResourceTemplateRepository>();
        services.TryAddScoped<IApplicationRepository, ApplicationRepository>();
        services.TryAddScoped<IEnvironmentRepository, EnvironmentRepository>();
        services.TryAddScoped<IDeploymentRepository, DeploymentRepository>();
        services.TryAddScoped<IResourceRepository, ResourceRepository>();
        services.TryAddScoped<IResourceInstanceRepository, ResourceInstanceRepository>();
        services.TryAddScoped<IResourceDependencyGraphRepository, ResourceDependencyGraphRepository>();

        return services;
    }

    public static async Task ApplyMigrations(this IServiceCollection services)
    {
        using IServiceScope scope = services.BuildServiceProvider().CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<OrchitectDbContext>();
        await dbContext.Database.MigrateAsync();
    }
}