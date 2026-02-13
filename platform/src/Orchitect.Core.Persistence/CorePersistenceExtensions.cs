using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Orchitect.Core.Domain.Credential;
using Orchitect.Core.Domain.Organisation;
using Orchitect.Core.Persistence.Repositories;
using Orchitect.Core.Persistence.Services;

namespace Orchitect.Core.Persistence;

public static class CorePersistenceExtensions
{
    public static IServiceCollection AddCorePersistenceServices(this IServiceCollection services)
    {
        services.AddDbContext<CoreDbContext>();
        services.TryAddScoped<IOrganisationRepository, OrganisationRepository>();
        services.TryAddScoped<ICredentialRepository, CredentialRepository>();
        services.TryAddSingleton<IEncryptionService, AesEncryptionService>();
        return services;
    }

    public static async Task ApplyCoreMigrations(this IServiceCollection services)
    {
        using IServiceScope scope = services.BuildServiceProvider().CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<CoreDbContext>();
        await dbContext.Database.MigrateAsync();
    }
}