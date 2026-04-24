using Orchitect.Domain.Core;
using Orchitect.Domain.Inventory.Pipeline.Requests;
using Orchitect.Domain.Inventory.Shared;

namespace Orchitect.Domain.Inventory.Pipeline.Services;

public interface IPipelineRepository :
    IRepository<Pipeline, PipelineId>,
    IQueryRepository<Pipeline, PipelineQuery>
{
    Task BulkUpsertAsync(
        IEnumerable<Pipeline> pipelines,
        CancellationToken cancellationToken = default);
}