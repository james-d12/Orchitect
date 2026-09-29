using NGitLab;

namespace Orchitect.Infrastructure.Inventory.GitLab.Services;

public interface IGitLabConnectionService
{
    GitLabClient Client { get; }
}