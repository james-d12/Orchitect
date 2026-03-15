using Microsoft.Extensions.Logging;
using Orchitect.Domain.Inventory.Git;
using Orchitect.Domain.Inventory.Git.Service;
using Orchitect.Infrastructure.Inventory.AzureDevOps.Models;
using Orchitect.Infrastructure.Inventory.Shared.Extensions;
using Orchitect.Infrastructure.Inventory.Shared.Observability;

namespace Orchitect.Infrastructure.Inventory.AzureDevOps.Services;

public sealed class AzureDevOpsQueryService : IAzureDevOpsQueryService
{
    private readonly ILogger<AzureDevOpsQueryService> _logger;
    private readonly IRepositoryRepository _repositoryRepository;
    private readonly IPipelineRepository _pipelineRepository;

    public AzureDevOpsQueryService(
        ILogger<AzureDevOpsQueryService> logger,
        IRepositoryRepository repositoryRepository,
        IPipelineRepository pipelineRepository)
    {
        _logger = logger;
        _repositoryRepository = repositoryRepository;
        _pipelineRepository = pipelineRepository;
    }

    public AzureDevOpsRepository? GetRepository(string repositoryName)
    {
        using var activity = Tracing.StartActivity();
        _logger.LogInformation("Getting Azure DevOps repository with {Name}", repositoryName);

        var repositories = _repositoryRepository
            .GetAll()
            .Where(r => r.Platform == RepositoryPlatform.AzureDevOps)
            .ToList();

        var repository = repositories.FirstOrDefault(a => a.Name.EqualsCaseInsensitive(repositoryName));

        return repository is not null ? new AzureDevOpsRepository
        {
            Id = repository.Id,
            OrganisationId = repository.OrganisationId,
            Name = repository.Name,
            Url = repository.Url,
            DefaultBranch = repository.DefaultBranch,
            Owner = repository.Owner,
            Platform = repository.Platform,
            DiscoveredAt = repository.DiscoveredAt,
            UpdatedAt = repository.UpdatedAt,
            IsDisabled = false,
            IsInMaintenance = false
        } : null;
    }

    public AzureDevOpsPipeline? GetPipeline(string pipelineName)
    {
        using var activity = Tracing.StartActivity();
        _logger.LogInformation("Getting Azure DevOps Pipeline with {Name}", pipelineName);

        var pipelines = _pipelineRepository
            .GetAll()
            .Where(p => p.Platform == PipelinePlatform.AzureDevOps)
            .ToList();

        var pipeline = pipelines.FirstOrDefault(a => a.Name.EqualsCaseInsensitive(pipelineName));

        return pipeline is not null ? new AzureDevOpsPipeline
        {
            Id = pipeline.Id,
            OrganisationId = pipeline.OrganisationId,
            Name = pipeline.Name,
            Url = pipeline.Url,
            Owner = pipeline.Owner,
            Platform = pipeline.Platform,
            DiscoveredAt = pipeline.DiscoveredAt,
            UpdatedAt = pipeline.UpdatedAt,
            Path = string.Empty
        } : null;
    }
}