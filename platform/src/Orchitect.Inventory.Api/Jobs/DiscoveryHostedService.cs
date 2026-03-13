using Orchitect.Inventory.Infrastructure.Shared.Observability;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Orchitect.Inventory.Domain.Discovery;
using Orchitect.Core.Domain.Credential;

namespace Orchitect.Inventory.Api.Jobs;

public sealed class DiscoveryHostedService : BackgroundService
{
    private readonly ILogger<DiscoveryHostedService> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly TimeSpan _interval;

    public DiscoveryHostedService(
        ILogger<DiscoveryHostedService> logger,
        IServiceProvider serviceProvider,
        IConfiguration configuration)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;

        // Read interval from config, default 30 minutes
        var intervalMinutes = configuration.GetValue<int>("DiscoverySettings:IntervalMinutes", 30);
        _interval = TimeSpan.FromMinutes(intervalMinutes);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Discovery Hosted Service started. Interval: {Interval}", _interval);

        // Optional: delay first run to allow app to fully start
        await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            using var activity = Tracing.StartActivity();
            _logger.LogInformation("Discovery cycle starting at {Time}", DateTimeOffset.Now);

            try
            {
                await RunDiscoveryCycleAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Discovery cycle failed");
            }

            _logger.LogInformation(
                "Discovery cycle completed. Next run in {Interval} at {NextRun}",
                _interval,
                DateTimeOffset.Now.Add(_interval));

            await Task.Delay(_interval, stoppingToken);
        }

        _logger.LogInformation("Discovery Hosted Service stopped");
    }

    private async Task RunDiscoveryCycleAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();

        var configRepository = scope.ServiceProvider
            .GetRequiredService<IDiscoveryConfigurationRepository>();
        var credentialRepository = scope.ServiceProvider
            .GetRequiredService<ICredentialRepository>();
        var payloadResolver = scope.ServiceProvider
            .GetRequiredService<CredentialPayloadResolver>();
        var discoveryServices = scope.ServiceProvider
            .GetServices<IDiscoveryService>()
            .ToList();

        _logger.LogDebug("Fetching enabled discovery configurations...");

        // Get all enabled discovery configurations
        var configurations = await configRepository.GetEnabledConfigurationsAsync(cancellationToken);
        var configList = configurations.ToList();

        _logger.LogInformation("Found {Count} enabled discovery configurations", configList.Count);

        // Group by organisation for better logging
        var orgGroups = configList.GroupBy(c => c.OrganisationId);

        foreach (var orgGroup in orgGroups)
        {
            var organisationId = orgGroup.Key;
            var orgConfigs = orgGroup.ToList();

            _logger.LogInformation(
                "Processing {Count} discovery configurations for organisation {OrgId}",
                orgConfigs.Count,
                organisationId);

            foreach (var config in orgConfigs)
            {
                try
                {
                    await ProcessDiscoveryConfigurationAsync(
                        config,
                        credentialRepository,
                        payloadResolver,
                        discoveryServices,
                        cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(
                        ex,
                        "Error processing discovery config {ConfigId} for platform {Platform}, org {OrgId}",
                        config.Id,
                        config.Platform,
                        organisationId);
                    // Continue with next configuration
                }
            }

            _logger.LogInformation(
                "Completed discovery for organisation {OrgId}",
                organisationId);
        }
    }

    private async Task ProcessDiscoveryConfigurationAsync(
        DiscoveryConfiguration config,
        ICredentialRepository credentialRepository,
        CredentialPayloadResolver payloadResolver,
        List<IDiscoveryService> discoveryServices,
        CancellationToken cancellationToken)
    {
        _logger.LogDebug(
            "Processing discovery config {ConfigId}: {Platform} for org {OrgId}",
            config.Id,
            config.Platform,
            config.OrganisationId);

        // Get credential
        var credential = await credentialRepository.GetByIdAsync(config.CredentialId, cancellationToken);
        if (credential == null)
        {
            _logger.LogWarning(
                "Credential {CredentialId} not found for config {ConfigId}, skipping",
                config.CredentialId,
                config.Id);
            return;
        }

        // Validate credential belongs to same org (shouldn't happen due to FK, but defensive)
        if (credential.OrganisationId != config.OrganisationId)
        {
            _logger.LogError(
                "Credential {CredentialId} belongs to org {CredOrgId} but config {ConfigId} is for org {ConfigOrgId}",
                credential.Id,
                credential.OrganisationId,
                config.Id,
                config.OrganisationId);
            return;
        }

        // Find matching discovery service
        var service = discoveryServices.FirstOrDefault(s =>
            s.Platform.Equals(config.Platform.ToString(), StringComparison.OrdinalIgnoreCase));

        if (service == null)
        {
            _logger.LogWarning(
                "No discovery service registered for platform {Platform}, skipping config {ConfigId}",
                config.Platform,
                config.Id);
            return;
        }

        // Run discovery
        _logger.LogInformation(
            "Starting {Platform} discovery for organisation {OrgId} using credential '{CredentialName}'",
            config.Platform,
            config.OrganisationId,
            credential.Name);

        await service.DiscoverAsync(
            config,
            credential,
            cancellationToken);

        _logger.LogInformation(
            "Completed {Platform} discovery for organisation {OrgId}",
            config.Platform,
            config.OrganisationId);
    }
}