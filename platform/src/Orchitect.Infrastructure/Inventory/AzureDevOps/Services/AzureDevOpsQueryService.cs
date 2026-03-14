using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Orchitect.Infrastructure.Inventory.AzureDevOps.Constants;
using Orchitect.Infrastructure.Inventory.AzureDevOps.Models;
using Orchitect.Infrastructure.Inventory.Shared.Extensions;
using Orchitect.Infrastructure.Inventory.Shared.Observability;

namespace Orchitect.Infrastructure.Inventory.AzureDevOps.Services;

public sealed class AzureDevOpsQueryService : IAzureDevOpsQueryService
{
    private readonly ILogger<AzureDevOpsGitQueryService> _logger;
    private readonly IMemoryCache _memoryCache;

    public AzureDevOpsQueryService(ILogger<AzureDevOpsGitQueryService> logger, IMemoryCache memoryCache)
    {
        _logger = logger;
        _memoryCache = memoryCache;
    }

    public AzureDevOpsRepository? GetRepository(string repositoryName)
    {
        using var activity = Tracing.StartActivity();
        _logger.LogInformation("Getting Azure DevOps repository with {Name}", repositoryName);
        var azureDevOpsRepositories =
            _memoryCache.Get<List<AzureDevOpsRepository>>(AzureDevOpsCacheConstants.RepositoryCacheKey) ?? [];
        return azureDevOpsRepositories
            .FirstOrDefault(a => a.Name.EqualsCaseInsensitive(repositoryName));
    }

    public AzureDevOpsPipeline? GetPipeline(string pipelineName)
    {
        using var activity = Tracing.StartActivity();
        _logger.LogInformation("Getting Azure DevOps Pipeline with {Name}", pipelineName);
        var azureDevOpsPipelines =
            _memoryCache.Get<List<AzureDevOpsPipeline>>(AzureDevOpsCacheConstants.PipelineCacheKey) ?? [];

        return azureDevOpsPipelines
            .FirstOrDefault(a => a.Name.EqualsCaseInsensitive(pipelineName));
    }
}