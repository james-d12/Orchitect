using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Orchitect.Api.Shared;
using Orchitect.Domain.Core;
using Orchitect.Domain.Core.Organisation;

namespace Orchitect.Api.Endpoints.Core.Organisation;

public sealed class RemoveOrganisationMemberEndpoint : IEndpoint
{
    public static void Map(IEndpointRouteBuilder builder) => builder
        .MapDelete("/{id:guid}/members/{memberId:guid}", HandleAsync)
        .RequireOrganisationAccess()
        .WithSummary("Removes a member from an organisation. The last member can't be removed.");

    private static async Task<Results<NoContent, NotFound, Conflict<ErrorResponse>>> HandleAsync(
        [FromRoute]
        Guid id,
        [FromRoute]
        Guid memberId,
        [FromServices]
        IOrganisationRepository repository,
        [FromServices]
        IUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        var organisationId = new OrganisationId(id);
        var organisationUserId = new OrganisationUserId(memberId);

        return await unitOfWork.ExecuteAsync<Results<NoContent, NotFound, Conflict<ErrorResponse>>>(async token =>
        {
            await repository.LockAsync(organisationId, token);
            var organisation = await repository.GetWithUsersAndTeamsAsync(organisationId, token);

            if (organisation is null || organisation.Users.All(u => u.Id != organisationUserId))
            {
                return TypedResults.NotFound();
            }

            if (organisation.Users.Count == 1)
            {
                return TypedResults.Conflict(new ErrorResponse
                {
                    Errors = [new Error { Code = "LAST_MEMBER", Message = "The last member of an organisation can't be removed." }]
                });
            }

            await repository.UpdateUsersAsync(organisation.RemoveUser(organisationUserId), token);
            return TypedResults.NoContent();
        }, cancellationToken);
    }
}
