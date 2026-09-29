using NGitLab.Models;
using Orchitect.Domain.Core.Organisation;
using Orchitect.Infrastructure.Inventory.GitLab.Models;

namespace Orchitect.Infrastructure.Inventory.GitLab.Services;

public interface IGitLabService
{
    List<Project> GetProjects();
    List<GitLabPullRequest> GetPullRequests(OrganisationId organisationId);
    List<GitLabPipeline> GetPipelines(Project project, OrganisationId organisationId);
}