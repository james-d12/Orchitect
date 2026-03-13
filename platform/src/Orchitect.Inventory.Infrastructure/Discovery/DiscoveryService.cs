using System.Diagnostics;
using Orchitect.Inventory.Domain.Discovery;
using Microsoft.Extensions.Logging;
using Orchitect.Inventory.Infrastructure.Shared.Observability;
using Orchitect.Core.Domain.Credential;

namespace Orchitect.Inventory.Infrastructure.Discovery;

public abstract class DiscoveryService : IDiscoveryService
{
    private readonly ILogger<DiscoveryService> _logger;

    protected DiscoveryService(ILogger<DiscoveryService> logger)
    {
        _logger = logger;
    }

    public virtual string Platform => string.Empty;

    public async Task DiscoverAsync(
        DiscoveryConfiguration configuration,
        Credential credential,
        CancellationToken cancellationToken)
    {
        using var activity = Tracing.StartActivity();
        try
        {
            _logger.LogInformation(
                "{Platform} Discovery Service started for organisation {OrgId}.",
                Platform,
                configuration.OrganisationId);

            var stopWatch = new Stopwatch();
            stopWatch.Start();

            await StartAsync(configuration, credential, cancellationToken);

            stopWatch.Stop();
            var milliseconds = stopWatch.Elapsed.TotalMilliseconds;

            _logger.LogInformation(
                "{Platform} Discovery Service for org {OrgId} took: {Milliseconds} ms",
                Platform,
                configuration.OrganisationId,
                milliseconds);
        }
        catch (Exception exception)
        {
            activity?.RecordException(exception);
            _logger.LogError(
                exception,
                "Error occurred whilst trying to discover {Platform} resources for organisation {OrgId}.",
                Platform,
                configuration.OrganisationId);
            throw;
        }
    }

    protected abstract Task StartAsync(
        DiscoveryConfiguration configuration,
        Credential credential,
        CancellationToken cancellationToken);
}