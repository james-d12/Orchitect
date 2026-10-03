using Orchitect.Domain.Core.Organisation;

namespace Orchitect.Api.Shared.Authorization;

public interface IOrganisationAccess
{
    /// <summary>
    /// Returns the ids of the organisations the current user is a member of, loaded once per request.
    /// </summary>
    Task<IReadOnlySet<OrganisationId>> GetOrganisationIdsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns whether the current user is a member of the organisation.
    /// </summary>
    Task<bool> IsMemberAsync(OrganisationId organisationId, CancellationToken cancellationToken = default);
}