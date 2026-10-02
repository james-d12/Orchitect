using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Orchitect.Api.Shared;
using Orchitect.Domain.Core.Credential;
using Orchitect.Domain.Core.Organisation;

namespace Orchitect.Api.Endpoints.Core.Credential;

public sealed class DeleteCredentialEndpoint : IEndpoint
{
    public static void Map(IEndpointRouteBuilder builder) => builder
        .MapDelete("/{id:guid}", HandleAsync)
        .WithSummary("Deletes a credential by Id.");

    private static async Task<Results<NoContent, NotFound>> HandleAsync(
        [FromRoute]
        Guid id,
        [FromServices]
        ICredentialRepository repository,
        [FromServices]
        IOrganisationRepository organisationRepository,
        ClaimsPrincipal user,
        CancellationToken cancellationToken)
    {
        var credentialId = new CredentialId(id);
        var existing = await repository.GetByIdAsync(credentialId, cancellationToken);

        if (existing is null ||
            !await organisationRepository.IsMemberAsync(user, existing.OrganisationId, cancellationToken))
        {
            return TypedResults.NotFound();
        }

        var deleted = await repository.DeleteAsync(credentialId, cancellationToken);

        if (!deleted)
        {
            return TypedResults.NotFound();
        }

        return TypedResults.NoContent();
    }
}