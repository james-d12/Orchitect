using Orchitect.Engine.Domain.Application;
using Orchitect.Engine.Domain.Deployment;
using Orchitect.Engine.Domain.Environment;
using Orchitect.Engine.Domain.Organisation;
using Orchitect.Engine.Domain.ResourceTemplate;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Orchitect.Engine.Persistence.Repositories;

namespace Orchitect.Engine.Persistence;

public static class EnginePersistenceExtensions
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddPersistenceServices()
        {
            services.AddDbContext<EngineDbContext>();
            services.AddScoped<IResourceTemplateRepository, ResourceTemplateRepository>();
            services.AddScoped<IApplicationRepository, ApplicationRepository>();
            services.AddScoped<IEnvironmentRepository, EnvironmentRepository>();
            services.AddScoped<IDeploymentRepository, DeploymentRepository>();
            services.AddScoped<IOrganisationRepository, OrganisationRepository>();

            return services;
        }

        public async Task ApplyMigrations()
        {
            using IServiceScope scope = services.BuildServiceProvider().CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<EngineDbContext>();
            await dbContext.Database.MigrateAsync();
        }
    }
}