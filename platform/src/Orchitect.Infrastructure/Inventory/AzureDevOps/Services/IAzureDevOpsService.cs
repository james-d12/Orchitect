using Orchitect.Domain.Core.Organisation;
using Orchitect.Infrastructure.Inventory.AzureDevOps.Models;

namespace Orchitect.Infrastructure.Inventory.AzureDevOps.Services;

public interface IAzureDevOpsService
{
    Task<List<AzureDevOpsRepository>> GetRepositoriesAsync(Guid projectId, OrganisationId organisationId, CancellationToken cancellationToken);

    Task<List<AzureDevOpsPipeline>> GetPipelinesAsync(
        Guid projectId,
        Uri projectUri,
        OrganisationId organisationId,
        CancellationToken cancellationToken);

    Task<List<AzureDevOpsProject>> GetProjectsAsync(
        string organisation,
        List<string> projectFilters,
        CancellationToken cancellationToken);

    Task<List<AzureDevOpsTeam>> GetTeamsAsync(CancellationToken cancellationToken);

    Task<List<AzureDevOpsWorkItem>> GetWorkItemsAsync(
        string projectName,
        Uri projectUri,
        OrganisationId organisationId,
        CancellationToken cancellationToken);

    Task<List<AzureDevOpsPullRequest>> GetPullRequestsAsync(
        Guid projectId,
        Uri projectUri,
        OrganisationId organisationId,
        CancellationToken cancellationToken);
}