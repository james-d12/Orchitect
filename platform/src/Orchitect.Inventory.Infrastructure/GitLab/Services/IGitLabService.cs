using NGitLab.Models;
using Orchitect.Inventory.Infrastructure.GitLab.Models;

namespace Orchitect.Inventory.Infrastructure.GitLab.Services;

public interface IGitLabService
{
    List<Project> GetProjects();
    List<GitLabPullRequest> GetPullRequests();
    List<GitLabPipeline> GetPipelines(Project project);
}