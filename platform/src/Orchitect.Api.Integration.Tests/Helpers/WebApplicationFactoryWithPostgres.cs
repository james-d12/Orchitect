using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Orchitect.Engine.Dispatch.Executor;
using Testcontainers.PostgreSql;

namespace Orchitect.Api.Integration.Tests.Helpers;

public sealed class WebApplicationFactoryWithPostgres : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:15.1").Build();

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

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
        Environment.SetEnvironmentVariable("ConnectionStrings__orchitect", _postgres.GetConnectionString());
    }

    public new async Task DisposeAsync()
    {
        Environment.SetEnvironmentVariable("ConnectionStrings__orchitect", null);
        await _postgres.DisposeAsync();
    }
}