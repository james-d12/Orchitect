using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Orchitect.Inventory.Domain.Discovery;
using Orchitect.Inventory.Api.Extensions;
using Orchitect.Core.Domain.Credential;
using Orchitect.Shared;
using System.Security.Claims;

namespace Orchitect.Inventory.Api.Endpoints.Discovery;

public sealed class CreateDiscoveryConfigurationEndpoint : IEndpoint
{
    public record Request(
        CredentialId CredentialId,
        DiscoveryPlatform Platform,
        bool IsEnabled,
        Dictionary<string, string>? PlatformConfig);

    public record Response(DiscoveryConfigurationId Id);

    public static void Map(IEndpointRouteBuilder builder) => builder
        .MapPost("/", HandleAsync)
        .WithName("CreateDiscoveryConfiguration")
        .WithSummary("Create a discovery configuration for the current organisation")
        .WithDescription("Links a credential to a discovery platform with optional configuration")
        .Produces<Response>(StatusCodes.Status200OK)
        .Produces<ErrorResponse>(StatusCodes.Status400BadRequest);

    private static async Task<Results<Ok<Response>, BadRequest<ErrorResponse>>> HandleAsync(
        [FromBody] Request request,
        [FromServices] IDiscoveryConfigurationRepository configRepository,
        [FromServices] ICredentialRepository credentialRepository,
        ClaimsPrincipal user,
        CancellationToken cancellationToken)
    {
        var organisationId = user.GetOrganisationId();

        // Validate credential exists and belongs to this organisation
        var credential = await credentialRepository.GetByIdAsync(request.CredentialId, cancellationToken);
        if (credential == null)
            return TypedResults.BadRequest(CreateError("CREDENTIAL_NOT_FOUND", "Credential not found"));

        if (credential.OrganisationId != organisationId)
            return TypedResults.BadRequest(CreateError("CREDENTIAL_ACCESS_DENIED", "Credential does not belong to your organisation"));

        // Validate platform matches credential platform
        var expectedPlatform = request.Platform.ToString();
        if (credential.Platform.ToString() != expectedPlatform)
            return TypedResults.BadRequest(
                CreateError("PLATFORM_MISMATCH", $"Credential platform ({credential.Platform}) does not match discovery platform ({request.Platform})"));

        // Create configuration
        var config = DiscoveryConfiguration.Create(
            organisationId,
            request.CredentialId,
            request.Platform,
            request.IsEnabled,
            request.PlatformConfig);

        await configRepository.CreateAsync(config, cancellationToken);

        return TypedResults.Ok(new Response(config.Id));
    }

    private static ErrorResponse CreateError(string code, string message) =>
        new() { Errors = [new Error { Code = code, Message = message }] };
}
