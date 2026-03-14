using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Orchitect.Api.Extensions;
using Orchitect.Domain.Inventory.Discovery;
using Orchitect.Shared;

namespace Orchitect.Api.Endpoints.Inventory.Discovery;

public sealed class UpdateDiscoveryConfigurationEndpoint : IEndpoint
{
    public record Request(
        bool IsEnabled,
        Dictionary<string, string>? PlatformConfig);

    public static void Map(IEndpointRouteBuilder builder) => builder
        .MapPut("/{id}", HandleAsync)
        .WithName("UpdateDiscoveryConfiguration")
        .WithSummary("Update a discovery configuration")
        .Produces(StatusCodes.Status200OK)
        .Produces<ErrorResponse>(StatusCodes.Status404NotFound);

    private static async Task<Results<Ok, NotFound<ErrorResponse>>> HandleAsync(
        [FromRoute]
        Guid id,
        [FromBody]
        Request request,
        [FromServices]
        IDiscoveryConfigurationRepository repository,
        ClaimsPrincipal user,
        CancellationToken cancellationToken)
    {
        var organisationId = user.GetOrganisationId();
        var configId = new DiscoveryConfigurationId(id);

        var existing = await repository.GetByIdAsync(configId, cancellationToken);
        if (existing == null || existing.OrganisationId != organisationId)
            return TypedResults.NotFound(CreateError("CONFIG_NOT_FOUND", "Discovery configuration not found"));

        var updated = existing.Update(request.IsEnabled, request.PlatformConfig);
        await repository.UpdateAsync(updated, cancellationToken);

        return TypedResults.Ok();
    }

    private static ErrorResponse CreateError(string code, string message) =>
        new() { Errors = [new Error { Code = code, Message = message }] };
}