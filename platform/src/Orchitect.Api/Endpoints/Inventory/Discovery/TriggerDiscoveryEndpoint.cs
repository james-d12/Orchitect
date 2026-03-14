using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Orchitect.Api.Extensions;
using Orchitect.Api.Shared;
using Orchitect.Domain.Core.Credential;
using Orchitect.Domain.Inventory.Discovery;

namespace Orchitect.Api.Endpoints.Inventory.Discovery;

public sealed class TriggerDiscoveryEndpoint : IEndpoint
{
    public static void Map(IEndpointRouteBuilder builder) => builder
        .MapPost("/{id}/trigger", HandleAsync)
        .WithName("TriggerDiscovery")
        .WithSummary("Manually trigger discovery for a specific configuration")
        .Produces(StatusCodes.Status202Accepted)
        .Produces<ErrorResponse>(StatusCodes.Status404NotFound)
        .Produces<ErrorResponse>(StatusCodes.Status400BadRequest);

    private static async Task<Results<Accepted, NotFound<ErrorResponse>, BadRequest<ErrorResponse>>> HandleAsync(
        [FromRoute]
        Guid id,
        [FromServices]
        IDiscoveryConfigurationRepository configRepository,
        [FromServices]
        ICredentialRepository credentialRepository,
        [FromServices]
        IEnumerable<IDiscoveryService> discoveryServices,
        ClaimsPrincipal user,
        CancellationToken cancellationToken)
    {
        var organisationId = user.GetOrganisationId();
        var configId = new DiscoveryConfigurationId(id);

        var config = await configRepository.GetByIdAsync(configId, cancellationToken);
        if (config == null || config.OrganisationId != organisationId)
            return TypedResults.NotFound(CreateError("CONFIG_NOT_FOUND", "Discovery configuration not found"));

        // Get credential
        var credential = await credentialRepository.GetByIdAsync(config.CredentialId, cancellationToken);
        if (credential == null)
            return TypedResults.BadRequest(CreateError("CREDENTIAL_NOT_FOUND", "Associated credential not found"));

        // Find matching discovery service
        var service = discoveryServices.FirstOrDefault(s =>
            s.Platform.Equals(config.Platform.ToString(), StringComparison.OrdinalIgnoreCase));

        if (service == null)
            return TypedResults.BadRequest(
                CreateError("SERVICE_NOT_FOUND", $"No discovery service available for platform {config.Platform}"));

        // Trigger discovery (fire and forget - could use background queue)
        _ = Task.Run(async () =>
        {
            try
            {
                await service.DiscoverAsync(
                    config,
                    credential,
                    CancellationToken.None);
            }
            catch (Exception ex)
            {
                // Log error (inject ILogger if needed)
                Console.WriteLine($"Discovery failed: {ex.Message}");
            }
        }, cancellationToken);

        return TypedResults.Accepted($"/api/discovery/{id}");
    }

    private static ErrorResponse CreateError(string code, string message) =>
        new() { Errors = [new Error { Code = code, Message = message }] };
}