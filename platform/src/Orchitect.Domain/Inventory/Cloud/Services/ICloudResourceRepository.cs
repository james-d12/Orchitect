using Orchitect.Domain.Core;
using Orchitect.Domain.Inventory.Cloud.Requests;
using Orchitect.Domain.Inventory.Shared;

namespace Orchitect.Domain.Inventory.Cloud.Services;

public interface ICloudResourceRepository :
    IRepository<CloudResource, CloudResourceId>,
    IQueryRepository<CloudResource, CloudResourceQuery>
{
    Task BulkUpsertAsync(
        IEnumerable<CloudResource> cloudResources,
        CancellationToken cancellationToken = default);
}