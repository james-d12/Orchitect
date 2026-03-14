using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Orchitect.Api.Extensions;
using Orchitect.Domain.Core.Credential;
using Orchitect.Domain.Inventory.Discovery;
using Orchitect.Shared;

namespace Orchitect.Api.Endpoints.Inventory.Discovery;

public sealed class CreateDiscoveryConfigurationEndpoint : IEndpoint
{
    public record CreateDiscoveryConfigurationRequest(
        CredentialId CredentialId,
        DiscoveryPlatform Platform,
        bool IsEnabled,
        Dictionary<string, string>? PlatformConfig);

    public record CreateDiscoveryConfigurationResponse(DiscoveryConfigurationId Id);

    public static void Map(IEndpointRouteBuilder builder) => builder
        .MapPost("/", HandleAsync)
        .WithName("CreateDiscoveryConfiguration")
        .WithSummary("Create a discovery configuration for the current organisation")
        .WithDescription("Links a credential to a discovery platform with optional configuration")
        .Produces<CreateDiscoveryConfigurationResponse>(StatusCodes.Status200OK)
        .Produces<ErrorResponse>(StatusCodes.Status400BadRequest);

    private static async Task<Results<Ok<CreateDiscoveryConfigurationResponse>, BadRequest<ErrorResponse>>> HandleAsync(
        [FromBody]
        CreateDiscoveryConfigurationRequest createDiscoveryConfigurationRequest,
        [FromServices]
        IDiscoveryConfigurationRepository configRepository,
        [FromServices]
        ICredentialRepository credentialRepository,
        ClaimsPrincipal user,
        CancellationToken cancellationToken)
    {
        var organisationId = user.GetOrganisationId();

        // Validate credential exists and belongs to this organisation
        var credential = await credentialRepository.GetByIdAsync(createDiscoveryConfigurationRequest.CredentialId, cancellationToken);
        if (credential == null)
            return TypedResults.BadRequest(CreateError("CREDENTIAL_NOT_FOUND", "Credential not found"));

        if (credential.OrganisationId != organisationId)
            return TypedResults.BadRequest(CreateError("CREDENTIAL_ACCESS_DENIED",
                "Credential does not belong to your organisation"));

        // Validate platform matches credential platform
        var expectedPlatform = createDiscoveryConfigurationRequest.Platform.ToString();
        if (credential.Platform.ToString() != expectedPlatform)
            return TypedResults.BadRequest(
                CreateError("PLATFORM_MISMATCH",
                    $"Credential platform ({credential.Platform}) does not match discovery platform ({createDiscoveryConfigurationRequest.Platform})"));

        // Create configuration
        var config = DiscoveryConfiguration.Create(
            organisationId,
            createDiscoveryConfigurationRequest.CredentialId,
            createDiscoveryConfigurationRequest.Platform,
            createDiscoveryConfigurationRequest.IsEnabled,
            createDiscoveryConfigurationRequest.PlatformConfig);

        await configRepository.CreateAsync(config, cancellationToken);

        return TypedResults.Ok(new CreateDiscoveryConfigurationResponse(config.Id));
    }

    private static ErrorResponse CreateError(string code, string message) =>
        new() { Errors = [new Error { Code = code, Message = message }] };
}