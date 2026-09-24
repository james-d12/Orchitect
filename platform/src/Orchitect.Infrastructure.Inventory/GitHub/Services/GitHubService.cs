using Orchitect.Common.Observability;
using Orchitect.Domain.Core.Organisation;
using Orchitect.Infrastructure.Inventory.GitHub.Extensions;
using Orchitect.Infrastructure.Inventory.GitHub.Models;

namespace Orchitect.Infrastructure.Inventory.GitHub.Services;

public sealed class GitHubService : IGitHubService
{
    private readonly IGitHubConnectionService _gitHubConnectionService;

    public GitHubService(IGitHubConnectionService gitHubConnectionService)
    {
        _gitHubConnectionService = gitHubConnectionService;
    }

    public async Task<List<GitHubRepository>> GetRepositoriesAsync(OrganisationId organisationId)
    {
        using var activity = Tracing.StartActivity();
        var repositories =
            await _gitHubConnectionService.Client.Repository.GetAllForCurrent() ?? [];
        return repositories.Select(r => r.MapToGitHubRepository(organisationId)).ToList();
    }

    public async Task<List<GitHubPipeline>> GetPipelinesAsync(GitHubRepository repository)
    {
        using var activity = Tracing.StartActivity();
        var pipelines =
            await _gitHubConnectionService.Client.Actions.Workflows.List(repository.User.Name, repository.Name);
        return pipelines.Workflows.Select(w => w.MapToGitHubPipeline(repository)).ToList();
    }

    public async Task<List<GitHubPullRequest>> GetPullRequestsAsync(GitHubRepository repository)
    {
        using var activity = Tracing.StartActivity();
        var isParsed = long.TryParse(repository.Id.Value, out var repositoryId);

        if (!isParsed)
        {
            return [];
        }

        var pullRequests = await _gitHubConnectionService.Client.PullRequest.GetAllForRepository(repositoryId);
        return pullRequests.Select(p => p.MapToGitHubPullRequest(repository)).ToList();
    }
}