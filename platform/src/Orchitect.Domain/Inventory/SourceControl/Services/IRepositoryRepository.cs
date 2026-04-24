using Orchitect.Domain.Core;
using Orchitect.Domain.Inventory.SourceControl.Requests;
using Orchitect.Domain.Inventory.Shared;

namespace Orchitect.Domain.Inventory.SourceControl.Services;

public interface IRepositoryRepository :
    IRepository<Repository, RepositoryId>,
    IQueryRepository<Repository, RepositoryQuery>
{
    Task BulkUpsertAsync(
        IEnumerable<Repository> repositories,
        CancellationToken cancellationToken = default);
}