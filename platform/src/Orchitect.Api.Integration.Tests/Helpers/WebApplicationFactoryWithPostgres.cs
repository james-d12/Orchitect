using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Orchitect.Engine.Dispatch.Executor;
using Orchitect.Persistence;
using Testcontainers.PostgreSql;

namespace Orchitect.Api.Integration.Tests.Helpers;

public sealed class WebApplicationFactoryWithPostgres : WebApplicationFactory<Program>, IAsyncLifetime
{
    private static readonly PostgreSqlContainer Postgres = new PostgreSqlBuilder("postgres:15.1").Build();
    private static readonly Lazy<Task> SharedDatabase = new(StartDatabaseAsync);

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["JwtOptions:Issuer"] = "orchitect-integration-tests",
                ["JwtOptions:Audience"] = "orchitect-integration-tests",
                ["JwtOptions:ExpirationInMinutes"] = "60",
                ["JwtOptions:Secret"] = "integration-test-jwt-secret-key-orchitect-platform",
                ["EncryptionOptions:Key"] = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA=",
                ["ExecutorOptions:ApiBaseUrl"] = "http://localhost:41005"
            });
        });

        builder.ConfigureTestServices(services =>
        {
            var sweep = services.Single(d => d.ServiceType == typeof(IHostedService) &&
                                             d.ImplementationType == typeof(RunnerContainerSweepService));
            services.Remove(sweep);
        });
    }

    public Task InitializeAsync() => SharedDatabase.Value;

    public new Task DisposeAsync() => base.DisposeAsync().AsTask();

    private static async Task StartDatabaseAsync()
    {
        await Postgres.StartAsync();
        var connectionString = Postgres.GetConnectionString();

        var options = new DbContextOptionsBuilder<OrchitectDbContext>().UseNpgsql(connectionString).Options;
        await using var dbContext = new OrchitectDbContext(options);
        await dbContext.Database.MigrateAsync();

        Environment.SetEnvironmentVariable("ConnectionStrings__orchitect", connectionString);
    }
}