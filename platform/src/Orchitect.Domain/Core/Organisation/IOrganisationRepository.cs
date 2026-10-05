namespace Orchitect.Domain.Core.Organisation;

public interface IOrganisationRepository : IRepository<Organisation, OrganisationId>
{
    Task<Organisation?> UpdateAsync(Organisation organisation, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(OrganisationId id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the organisations with the given ids.
    /// </summary>
    Task<IReadOnlyList<Organisation>> GetByIdsAsync(IReadOnlyCollection<OrganisationId> ids,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the organisation with its users and teams.
    /// </summary>
    Task<Organisation?> GetWithUsersAndTeamsAsync(OrganisationId id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Locks the organisation's row until the current transaction ends.
    /// </summary>
    Task LockAsync(OrganisationId id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves the organisation's members, adding new ones and deleting removed ones.
    /// </summary>
    Task UpdateUsersAsync(Organisation organisation, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the ids of the organisations the identity user is a member of.
    /// </summary>
    Task<IReadOnlyList<OrganisationId>> GetIdsForMemberAsync(string identityUserId,
        CancellationToken cancellationToken = default);
}