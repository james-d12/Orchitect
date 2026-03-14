using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Orchitect.Domain.Core.Credential;
using Orchitect.Domain.Core.Organisation;
using Orchitect.Domain.Engine.Application;
using Orchitect.Domain.Engine.Deployment;
using Orchitect.Domain.Engine.Environment;
using Orchitect.Domain.Engine.ResourceTemplate;
using Orchitect.Domain.Inventory.Discovery;
using Orchitect.Persistence.Repositories.Core;
using Orchitect.Persistence.Repositories.Engine;
using Orchitect.Persistence.Repositories.Inventory;

namespace Orchitect.Persistence;

public static class OrchitectPersistenceExtensions
{
    public static IServiceCollection AddPersistenceServices(this IServiceCollection services)
    {
        services.AddDbContext<OrchitectDbContext>();

        services.TryAddScoped<IOrganisationRepository, OrganisationRepository>();
        services.TryAddScoped<ICredentialRepository, CredentialRepository>();

        services.TryAddScoped<IDiscoveryConfigurationRepository, DiscoveryConfigurationRepository>();

        services.TryAddScoped<IResourceTemplateRepository, ResourceTemplateRepository>();
        services.TryAddScoped<IApplicationRepository, ApplicationRepository>();
        services.TryAddScoped<IEnvironmentRepository, EnvironmentRepository>();
        services.TryAddScoped<IDeploymentRepository, DeploymentRepository>();

        return services;
    }

    public static async Task ApplyMigrations(this IServiceCollection services)
    {
        using IServiceScope scope = services.BuildServiceProvider().CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<OrchitectDbContext>();
        await dbContext.Database.MigrateAsync();
    }
}