namespace Orchitect.Domain.Core.Organisation;

public interface IOrganisationRepository : IRepository<Organisation, OrganisationId>
{
    Task<Organisation?> UpdateAsync(Organisation organisation, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(OrganisationId id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns whether the identity user is a member of the organisation.
    /// </summary>
    Task<bool> IsMemberAsync(OrganisationId id, string identityUserId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the ids of the organisations the identity user is a member of.
    /// </summary>
    Task<IReadOnlyList<OrganisationId>> GetIdsForMemberAsync(string identityUserId,
        CancellationToken cancellationToken = default);
}