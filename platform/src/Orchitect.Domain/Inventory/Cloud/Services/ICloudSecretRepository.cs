using Orchitect.Domain.Core;
using Orchitect.Domain.Inventory.Cloud.Requests;
using Orchitect.Domain.Inventory.Shared;

namespace Orchitect.Domain.Inventory.Cloud.Services;

public interface ICloudSecretRepository :
    IRepository<CloudSecret, CloudSecretId>,
    IQueryRepository<CloudSecret, CloudSecretQuery>
{
    Task BulkUpsertAsync(
        IEnumerable<CloudSecret> cloudSecrets,
        CancellationToken cancellationToken = default);
}