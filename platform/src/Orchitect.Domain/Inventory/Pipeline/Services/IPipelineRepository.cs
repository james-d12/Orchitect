using Orchitect.Domain.Core;
using Orchitect.Domain.Core.Organisation;

namespace Orchitect.Domain.Inventory.Pipeline.Services;

public interface IPipelineRepository : IRepository<Pipeline, PipelineId>
{
    Task<IReadOnlyList<Pipeline>> GetByOrganisationIdAsync(
        OrganisationId organisationId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Pipeline>> GetByPlatformAsync(
        OrganisationId organisationId,
        PipelinePlatform platform,
        CancellationToken cancellationToken = default);

    Task BulkUpsertAsync(
        IEnumerable<Pipeline> pipelines,
        CancellationToken cancellationToken = default);
}
