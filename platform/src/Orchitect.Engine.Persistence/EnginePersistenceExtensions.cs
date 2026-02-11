using Microsoft.EntityFrameworkCore;
using Orchitect.Engine.Domain.Application;
using Orchitect.Engine.Domain.Deployment;
using Orchitect.Engine.Domain.Environment;
using Orchitect.Engine.Domain.ResourceTemplate;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Orchitect.Engine.Persistence.Repositories;

namespace Orchitect.Engine.Persistence;

public static class EnginePersistenceExtensions
{
    public static IServiceCollection AddEnginePersistenceServices(this IServiceCollection services)
    {
        services.AddDbContext<EngineDbContext>();
        services.TryAddScoped<IResourceTemplateRepository, ResourceTemplateRepository>();
        services.TryAddScoped<IApplicationRepository, ApplicationRepository>();
        services.TryAddScoped<IEnvironmentRepository, EnvironmentRepository>();
        services.TryAddScoped<IDeploymentRepository, DeploymentRepository>();
        return services;
    }

    public static async Task ApplyEngineMigrations(this IServiceCollection services)
    {
        using IServiceScope scope = services.BuildServiceProvider().CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<EngineDbContext>();
        await dbContext.Database.MigrateAsync();
    }
}