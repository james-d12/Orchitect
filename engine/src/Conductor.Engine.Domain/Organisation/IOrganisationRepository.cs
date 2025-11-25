namespace Conductor.Engine.Domain.Organisation;

public interface IOrganisationRepository
{
    Task<Organisation?> CreateAsync(Organisation environment,
        CancellationToken cancellationToken = default);

    IEnumerable<Organisation> GetAll();
    Task<Organisation?> GetByIdAsync(OrganisationId id, CancellationToken cancellationToken = default);
}