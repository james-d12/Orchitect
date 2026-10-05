using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Orchitect.Api.Shared;
using Orchitect.Domain.Core;
using Orchitect.Domain.Core.Organisation;

namespace Orchitect.Api.Endpoints.Core.Organisation;

public sealed class AddOrganisationMemberEndpoint : IEndpoint
{
    public static void Map(IEndpointRouteBuilder builder) => builder
        .MapPost("/{id:guid}/members", HandleAsync)
        .RequireOrganisationAccess()
        .WithSummary("Adds a registered user to an organisation by email.");

    public sealed record AddOrganisationMemberRequest(string Email);

    private static async Task<Results<Ok<OrganisationMemberResponse>, NotFound, BadRequest<ErrorResponse>,
        Conflict<ErrorResponse>>> HandleAsync(
        [FromRoute]
        Guid id,
        [FromBody]
        AddOrganisationMemberRequest request,
        [FromServices]
        IOrganisationRepository repository,
        [FromServices]
        IUnitOfWork unitOfWork,
        [FromServices]
        UserManager<IdentityUser> userManager,
        CancellationToken cancellationToken)
    {
        var user = string.IsNullOrWhiteSpace(request.Email) ? null : await userManager.FindByEmailAsync(request.Email);

        if (user is null)
        {
            return TypedResults.BadRequest(new ErrorResponse
            {
                Errors = [new Error { Code = "USER_NOT_FOUND", Message = "No user is registered with that email." }]
            });
        }

        var organisationId = new OrganisationId(id);

        return await unitOfWork.ExecuteAsync<Results<Ok<OrganisationMemberResponse>, NotFound,
            BadRequest<ErrorResponse>, Conflict<ErrorResponse>>>(async token =>
        {
            await repository.LockAsync(organisationId, token);
            var organisation = await repository.GetWithUsersAndTeamsAsync(organisationId, token);

            if (organisation is null)
            {
                return TypedResults.NotFound();
            }

            if (organisation.Users.Any(u => u.IdentityUserId == user.Id))
            {
                return TypedResults.Conflict(new ErrorResponse
                {
                    Errors = [new Error { Code = "ALREADY_A_MEMBER", Message = "The user is already a member." }]
                });
            }

            var updatedOrganisation = organisation.AddUser(user.Id);
            await repository.UpdateUsersAsync(updatedOrganisation, token);

            var member = updatedOrganisation.Users.Single(u => u.IdentityUserId == user.Id);
            return TypedResults.Ok(OrganisationMemberResponse.From(member, user));
        }, cancellationToken);
    }
}
