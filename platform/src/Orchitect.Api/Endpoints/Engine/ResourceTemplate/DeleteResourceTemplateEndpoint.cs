using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Orchitect.Api.Shared;
using Orchitect.Domain.Core.Organisation;
using Orchitect.Domain.Engine.ResourceTemplate;

namespace Orchitect.Api.Endpoints.Engine.ResourceTemplate;

public sealed class DeleteResourceTemplateEndpoint : IEndpoint
{
    public static void Map(IEndpointRouteBuilder builder) => builder
        .MapDelete("/{id:guid}", HandleAsync)
        .WithSummary("Deletes a resource template by Id.");

    private static async Task<Results<NoContent, NotFound>> HandleAsync(
        [FromRoute]
        Guid id,
        [FromServices]
        IResourceTemplateRepository repository,
        [FromServices]
        IOrganisationRepository organisationRepository,
        ClaimsPrincipal user,
        CancellationToken cancellationToken)
    {
        var resourceTemplateId = new ResourceTemplateId(id);
        var existing = await repository.GetByIdAsync(resourceTemplateId, cancellationToken);

        if (existing is null ||
            !await organisationRepository.IsMemberAsync(user, existing.OrganisationId, cancellationToken))
        {
            return TypedResults.NotFound();
        }

        var deleted = await repository.DeleteAsync(resourceTemplateId, cancellationToken);

        if (!deleted)
        {
            return TypedResults.NotFound();
        }

        return TypedResults.NoContent();
    }
}
