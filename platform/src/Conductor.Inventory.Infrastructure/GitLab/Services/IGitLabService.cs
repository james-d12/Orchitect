using Conductor.Inventory.Infrastructure.GitLab.Models;
using NGitLab.Models;

namespace Conductor.Inventory.Infrastructure.GitLab.Services;

public interface IGitLabService
{
    List<Project> GetProjects();
    List<GitLabPullRequest> GetPullRequests();
    List<GitLabPipeline> GetPipelines(Project project);
}