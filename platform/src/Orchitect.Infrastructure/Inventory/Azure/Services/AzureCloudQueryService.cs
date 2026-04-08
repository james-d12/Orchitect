using Microsoft.Extensions.Logging;
using Orchitect.Domain.Inventory.Cloud;
using Orchitect.Domain.Inventory.Cloud.Requests;
using Orchitect.Domain.Inventory.Cloud.Services;
using Orchitect.Infrastructure.Inventory.Shared.Extensions;
using Orchitect.Infrastructure.Inventory.Shared.Observability;
using Orchitect.Infrastructure.Inventory.Shared.Query;

namespace Orchitect.Infrastructure.Inventory.Azure.Services;

public sealed class AzureCloudQueryService : ICloudQueryService
{
    private readonly ILogger<AzureCloudQueryService> _logger;
    private readonly ICloudResourceRepository _cloudResourceRepository;
    private readonly ICloudSecretRepository _cloudSecretRepository;

    public AzureCloudQueryService(
        ILogger<AzureCloudQueryService> logger,
        ICloudResourceRepository cloudResourceRepository,
        ICloudSecretRepository cloudSecretRepository)
    {
        _logger = logger;
        _cloudResourceRepository = cloudResourceRepository;
        _cloudSecretRepository = cloudSecretRepository;
    }

    public List<CloudResource> QueryCloudResources(CloudResourceQueryRequest request)
    {
        using var activity = Tracing.StartActivity();
        _logger.LogInformation("Querying cloud resources from database for organisation {OrganisationId}", request.OrganisationId);

        var cloudResources = _cloudResourceRepository
            .GetByPlatformAsync(request.OrganisationId, CloudPlatform.Azure)
            .GetAwaiter()
            .GetResult()
            .ToList();

        if (cloudResources.Count <= 0)
        {
            return [];
        }

        return new QueryBuilder<CloudResource>(cloudResources)
            .Where(request.Id, p => p.Id.Value == request.Id)
            .Where(request.Name, p => p.Name.Contains(request.Name ?? string.Empty))
            .Where(request.Description, p => p.Description.Contains(request.Description ?? string.Empty))
            .Where(request.Url, p => p.Url.ToString().Contains(request.Url ?? string.Empty))
            .Where(request.Type, p => p.Type.EqualsCaseInsensitive(request.Type))
            .Where(request.Platform, p => p.Platform == request.Platform)
            .ToList();
    }

    public List<CloudSecret> QueryCloudSecrets(CloudSecretQueryRequest request)
    {
        using var activity = Tracing.StartActivity();
        _logger.LogInformation("Querying cloud secrets from database for organisation {OrganisationId}", request.OrganisationId);

        var cloudSecrets = _cloudSecretRepository
            .GetByPlatformAsync(request.OrganisationId, CloudSecretPlatform.Azure)
            .GetAwaiter()
            .GetResult()
            .ToList();

        if (cloudSecrets.Count <= 0)
        {
            return [];
        }

        return new QueryBuilder<CloudSecret>(cloudSecrets)
            .Where(request.Name, p => p.Name.Contains(request.Name ?? string.Empty))
            .Where(request.Location, p => p.Location.Contains(request.Location ?? string.Empty))
            .Where(request.Url, p => p.Url.ToString().Contains(request.Url ?? string.Empty))
            .Where(request.Platform, p => p.Platform == request.Platform)
            .ToList();
    }
}