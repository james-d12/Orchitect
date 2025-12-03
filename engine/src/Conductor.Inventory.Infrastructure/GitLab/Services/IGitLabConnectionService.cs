using NGitLab;

namespace Conductor.Inventory.Infrastructure.GitLab.Services;

public interface IGitLabConnectionService
{
    GitLabClient Client { get; }
}