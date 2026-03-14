using NGitLab.Models;
using Orchitect.Infrastructure.Inventory.GitLab.Models;

namespace Orchitect.Infrastructure.Inventory.GitLab.Services;

public interface IGitLabService
{
    List<Project> GetProjects();
    List<GitLabPullRequest> GetPullRequests();
    List<GitLabPipeline> GetPipelines(Project project);
}