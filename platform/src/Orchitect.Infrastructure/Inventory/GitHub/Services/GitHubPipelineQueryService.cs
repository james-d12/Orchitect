using Microsoft.Extensions.Logging;
using Orchitect.Domain.Inventory.Pipeline;
using Orchitect.Domain.Inventory.Pipeline.Requests;
using Orchitect.Domain.Inventory.Pipeline.Services;
using Orchitect.Infrastructure.Inventory.Shared.Extensions;
using Orchitect.Infrastructure.Inventory.Shared.Observability;
using Orchitect.Infrastructure.Inventory.Shared.Query;

namespace Orchitect.Infrastructure.Inventory.GitHub.Services;

public sealed class GitHubPipelineQueryService : IPipelineQueryService
{
    private readonly ILogger<GitHubSourceControlQueryService> _logger;
    private readonly IPipelineRepository _pipelineRepository;

    public GitHubPipelineQueryService(
        ILogger<GitHubSourceControlQueryService> logger,
        IPipelineRepository pipelineRepository)
    {
        _logger = logger;
        _pipelineRepository = pipelineRepository;
    }

    public List<Pipeline> QueryPipelines(PipelineQueryRequest request)
    {
        using var activity = Tracing.StartActivity();
        _logger.LogInformation("Querying pipelines from database for organisation {OrganisationId}",
            request.OrganisationId);

        var pipelines = _pipelineRepository
            .GetByPlatformAsync(request.OrganisationId, PipelinePlatform.GitHub)
            .GetAwaiter()
            .GetResult()
            .ToList();

        return new QueryBuilder<Pipeline>(pipelines)
            .Where(request.Id, p => p.Id.Value.EqualsCaseInsensitive(request.Id))
            .Where(request.Name, p => p.Name.ContainsCaseInsensitive(request.Name))
            .Where(request.Url, p => p.Url.ToString().ContainsCaseInsensitive(request.Url))
            .Where(request.OwnerName, p => p.User.Name.EqualsCaseInsensitive(request.OwnerName))
            .Where(request.Platform, p => p.Platform == request.Platform)
            .ToList();
    }
}