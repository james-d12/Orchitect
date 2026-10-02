using System.Security.Claims;
using Orchitect.Domain.Core.Organisation;

namespace Orchitect.Api.Shared;

public static class OrganisationMembershipExtensions
{
    public static Task<bool> IsMemberAsync(this IOrganisationRepository repository, ClaimsPrincipal user,
        OrganisationId organisationId, CancellationToken cancellationToken = default)
    {
        return repository.IsMemberAsync(organisationId, user.GetUserId(), cancellationToken);
    }

    public static async Task<HashSet<OrganisationId>> GetMemberOrganisationIdsAsync(
        this IOrganisationRepository repository, ClaimsPrincipal user, CancellationToken cancellationToken = default)
    {
        var organisationIds = await repository.GetIdsForMemberAsync(user.GetUserId(), cancellationToken);
        return organisationIds.ToHashSet();
    }
}