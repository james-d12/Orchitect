using NGitLab;

namespace Orchitect.Inventory.Infrastructure.GitLab.Services;

public interface IGitLabConnectionService
{
    GitLabClient Client { get; }
}