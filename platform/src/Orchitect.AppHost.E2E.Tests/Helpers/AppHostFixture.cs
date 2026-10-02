using System.Security.Cryptography;
using Aspire.Hosting;

namespace Orchitect.AppHost.E2E.Tests.Helpers;

public sealed class AppHostFixture : IAsyncLifetime
{
    public static readonly TimeSpan StartupTimeout = TimeSpan.FromMinutes(5);

    private DistributedApplication? _app;

    public DistributedApplication App =>
        _app ?? throw new InvalidOperationException("The AppHost has not been started.");

    public async Task InitializeAsync()
    {
        using var cts = new CancellationTokenSource(StartupTimeout);

        var builder = await DistributedApplicationTestingBuilder.CreateAsync<Projects.Orchitect_AppHost>(
            ["Parameters:keyvault-uri=https://orchitect-e2e-tests.vault.azure.net/"],
            cts.Token);

        builder.CreateResourceBuilder<ProjectResource>(AppHostResources.Api)
            .WithEnvironment("JwtOptions__Secret", Convert.ToBase64String(RandomNumberGenerator.GetBytes(48)))
            .WithEnvironment("EncryptionOptions__Key", Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)));

        _app = await builder.BuildAsync(cts.Token);
        await _app.StartAsync(cts.Token);
    }

    public async Task DisposeAsync()
    {
        if (_app is not null)
        {
            await _app.DisposeAsync();
        }
    }
}
