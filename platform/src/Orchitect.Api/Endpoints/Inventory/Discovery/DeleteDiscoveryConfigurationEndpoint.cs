using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Orchitect.Api.Shared;
using Orchitect.Domain.Core.Organisation;
using Orchitect.Domain.Inventory.Discovery;
using Orchitect.Domain.Inventory.Discovery.Services;

namespace Orchitect.Api.Endpoints.Inventory.Discovery;

public sealed class DeleteDiscoveryConfigurationEndpoint : IEndpoint
{
    public static void Map(IEndpointRouteBuilder builder) => builder
        .MapDelete("/{id}", HandleAsync)
        .WithName("DeleteDiscoveryConfiguration")
        .WithSummary("Delete a discovery configuration")
        .Produces(StatusCodes.Status204NoContent)
        .Produces<ErrorResponse>(StatusCodes.Status404NotFound);

    private static async Task<Results<NoContent, NotFound<ErrorResponse>>> HandleAsync(
        [FromRoute]
        Guid id,
        [FromQuery]
        string organisationId,
        [FromServices]
        IDiscoveryConfigurationRepository repository,
        CancellationToken cancellationToken)
    {
        var orgId = new OrganisationId(Guid.Parse(organisationId));
        var configId = new DiscoveryConfigurationId(id);

        var existing = await repository.GetByIdAsync(configId, cancellationToken);
        if (existing == null || existing.OrganisationId != orgId)
            return TypedResults.NotFound(CreateError("CONFIG_NOT_FOUND", "Discovery configuration not found"));

        await repository.DeleteAsync(configId, cancellationToken);

        return TypedResults.NoContent();
    }

    private static ErrorResponse CreateError(string code, string message) =>
        new() { Errors = [new Error { Code = code, Message = message }] };
}