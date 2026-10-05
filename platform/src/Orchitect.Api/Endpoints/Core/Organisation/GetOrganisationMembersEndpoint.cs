using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Orchitect.Api.Shared;
using Orchitect.Domain.Core.Organisation;

namespace Orchitect.Api.Endpoints.Core.Organisation;

public sealed class GetOrganisationMembersEndpoint : IEndpoint
{
    public static void Map(IEndpointRouteBuilder builder) => builder
        .MapGet("/{id:guid}/members", HandleAsync)
        .RequireOrganisationAccess()
        .WithSummary("Gets the members of an organisation.");

    public sealed record GetOrganisationMembersResponse(List<OrganisationMemberResponse> Members);

    private static async Task<Results<Ok<GetOrganisationMembersResponse>, NotFound>> HandleAsync(
        [FromRoute]
        Guid id,
        [FromServices]
        IOrganisationRepository repository,
        [FromServices]
        UserManager<IdentityUser> userManager,
        CancellationToken cancellationToken)
    {
        var organisation = await repository.GetWithUsersAndTeamsAsync(new OrganisationId(id), cancellationToken);

        if (organisation is null)
        {
            return TypedResults.NotFound();
        }

        var identityUserIds = organisation.Users.Select(u => u.IdentityUserId).ToList();
        var users = await userManager.Users
            .Where(u => identityUserIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, cancellationToken);

        var members = organisation.Users
            .Select(u => OrganisationMemberResponse.From(u, users.GetValueOrDefault(u.IdentityUserId)))
            .ToList();

        return TypedResults.Ok(new GetOrganisationMembersResponse(members));
    }
}
