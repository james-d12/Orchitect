using Microsoft.Extensions.Logging;
using Orchitect.Domain.Core.Credential;
using Orchitect.Domain.Inventory.Discovery;
using Orchitect.Domain.Inventory.Discovery.Services;

namespace Orchitect.Api.Jobs;

public sealed class DiscoveryRunner
{
    private readonly IDiscoveryRunRepository _runRepository;
    private readonly ICredentialRepository _credentialRepository;
    private readonly IEnumerable<IDiscoveryService> _discoveryServices;
    private readonly ILogger<DiscoveryRunner> _logger;

    public DiscoveryRunner(
        IDiscoveryRunRepository runRepository,
        ICredentialRepository credentialRepository,
        IEnumerable<IDiscoveryService> discoveryServices,
        ILogger<DiscoveryRunner> logger)
    {
        _runRepository = runRepository;
        _credentialRepository = credentialRepository;
        _discoveryServices = discoveryServices;
        _logger = logger;
    }

    public async Task<DiscoveryRun> StartAsync(DiscoveryConfiguration configuration,
        CancellationToken cancellationToken)
    {
        var run = await _runRepository.CreateAsync(DiscoveryRun.Start(configuration.Id), cancellationToken);
        ArgumentNullException.ThrowIfNull(run);
        return run;
    }

    public async Task<DiscoveryRun> ExecuteAsync(DiscoveryConfiguration configuration, DiscoveryRun run,
        CancellationToken cancellationToken)
    {
        DiscoveryRun finished;

        try
        {
            finished = await DiscoverAsync(configuration, run, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            finished = run.Fail("Discovery was cancelled.");
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Discovery run {RunId} for config {ConfigId} failed", run.Id.Value,
                configuration.Id.Value);
            finished = run.Fail(exception.Message);
        }

        await _runRepository.UpdateAsync(finished, CancellationToken.None);

        _logger.LogInformation(
            "Discovery run {RunId} for {Platform} config {ConfigId} finished as {Status}",
            finished.Id.Value,
            configuration.Platform,
            configuration.Id.Value,
            finished.Status);

        return finished;
    }

    public async Task<DiscoveryRun> RunAsync(DiscoveryConfiguration configuration,
        CancellationToken cancellationToken)
    {
        var run = await StartAsync(configuration, cancellationToken);
        return await ExecuteAsync(configuration, run, cancellationToken);
    }

    private async Task<DiscoveryRun> DiscoverAsync(DiscoveryConfiguration configuration, DiscoveryRun run,
        CancellationToken cancellationToken)
    {
        var credential = await _credentialRepository.GetByIdAsync(configuration.CredentialId, cancellationToken);

        if (credential is null || credential.OrganisationId != configuration.OrganisationId)
        {
            return run.Fail($"Credential '{configuration.CredentialId.Value}' was not found.");
        }

        var service = _discoveryServices.FirstOrDefault(s => s.Platform == configuration.Platform);

        if (service is null)
        {
            return run.Fail($"No discovery service is registered for {configuration.Platform}.");
        }

        var counts = await service.DiscoverAsync(configuration, credential, cancellationToken);
        return run.Succeed(counts);
    }
}
