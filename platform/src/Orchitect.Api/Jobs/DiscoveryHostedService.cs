using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Orchitect.Common.Observability;
using Orchitect.Domain.Inventory.Discovery;
using Orchitect.Domain.Inventory.Discovery.Services;

namespace Orchitect.Api.Jobs;

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
        var intervalMinutes = configuration.GetValue("DiscoverySettings:IntervalMinutes", 30);
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
        List<DiscoveryConfiguration> configList;

        using (var scope = _serviceProvider.CreateScope())
        {
            var configRepository = scope.ServiceProvider
                .GetRequiredService<IDiscoveryConfigurationRepository>();

            _logger.LogDebug("Fetching enabled discovery configurations...");

            var configurations = await configRepository.GetEnabledConfigurationsAsync(cancellationToken);
            configList = configurations.ToList();
        }

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
                    using var scope = _serviceProvider.CreateScope();
                    var runner = scope.ServiceProvider.GetRequiredService<DiscoveryRunner>();
                    await runner.RunAsync(config, cancellationToken);
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
}