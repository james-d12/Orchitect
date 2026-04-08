using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Orchitect.Api.Shared;
using Orchitect.Domain.Core.Credential;
using Orchitect.Domain.Core.Organisation;
using Orchitect.Domain.Inventory.Discovery;
using Orchitect.Domain.Inventory.Discovery.Services;

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
        [FromQuery]
        string organisationId,
        [FromServices]
        IDiscoveryConfigurationRepository configRepository,
        [FromServices]
        ICredentialRepository credentialRepository,
        [FromServices]
        IServiceProvider serviceProvider,
        CancellationToken cancellationToken)
    {
        var orgId = new OrganisationId(Guid.Parse(organisationId));
        var configId = new DiscoveryConfigurationId(id);

        var config = await configRepository.GetByIdAsync(configId, cancellationToken);
        if (config == null || config.OrganisationId != orgId)
            return TypedResults.NotFound(CreateError("CONFIG_NOT_FOUND", "Discovery configuration not found"));

        // Get credential
        var credential = await credentialRepository.GetByIdAsync(config.CredentialId, cancellationToken);
        if (credential == null)
            return TypedResults.BadRequest(CreateError("CREDENTIAL_NOT_FOUND", "Associated credential not found"));

        // Trigger discovery in background with a new scope
        _ = Task.Run(async () =>
        {
            // Create a new scope for the background task to avoid disposed context
            using var scope = serviceProvider.CreateScope();
            var scopedServices = scope.ServiceProvider;

            try
            {
                // Get discovery services from the new scope
                var discoveryServices = scopedServices.GetRequiredService<IEnumerable<IDiscoveryService>>();

                // Find matching discovery service
                var service = discoveryServices.FirstOrDefault(s =>
                    s.Platform.Equals(config.Platform.ToString(), StringComparison.OrdinalIgnoreCase));

                if (service == null)
                {
                    Console.WriteLine($"No discovery service available for platform {config.Platform}");
                    return;
                }

                await service.DiscoverAsync(
                    config,
                    credential,
                    CancellationToken.None);
            }
            catch (Exception ex)
            {
                // Log error (inject ILogger if needed)
                Console.WriteLine($"Discovery failed: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
            }
        }, CancellationToken.None);

        return TypedResults.Accepted($"/api/discovery/{id}");
    }

    private static ErrorResponse CreateError(string code, string message) =>
        new() { Errors = [new Error { Code = code, Message = message }] };
}