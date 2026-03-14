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

public sealed class ListDiscoveryConfigurationsEndpoint : IEndpoint
{
    public record ListDiscoveryConfigurationResponse(
        DiscoveryConfigurationId Id,
        CredentialId CredentialId,
        string CredentialName,
        DiscoveryPlatform Platform,
        bool IsEnabled,
        Dictionary<string, string> PlatformConfig,
        DateTime CreatedAt,
        DateTime UpdatedAt);

    public static void Map(IEndpointRouteBuilder builder) => builder
        .MapGet("/", HandleAsync)
        .WithName("ListDiscoveryConfigurations")
        .WithSummary("List all discovery configurations for the current organisation")
        .Produces<IEnumerable<ListDiscoveryConfigurationResponse>>(StatusCodes.Status200OK);

    private static async Task<Ok<IEnumerable<ListDiscoveryConfigurationResponse>>> HandleAsync(
        [FromServices]
        IDiscoveryConfigurationRepository configRepository,
        [FromServices]
        ICredentialRepository credentialRepository,
        ClaimsPrincipal user,
        CancellationToken cancellationToken)
    {
        var organisationId = user.GetOrganisationId();

        var configs = await configRepository.GetByOrganisationIdAsync(organisationId, cancellationToken);

        // Join with credentials to get credential names
        var credentials = new Dictionary<CredentialId, string>();
        foreach (var config in configs)
        {
            if (!credentials.ContainsKey(config.CredentialId))
            {
                var cred = await credentialRepository.GetByIdAsync(config.CredentialId, cancellationToken);
                if (cred != null)
                    credentials[config.CredentialId] = cred.Name;
            }
        }

        var response = configs.Select(c => new ListDiscoveryConfigurationResponse(
            c.Id,
            c.CredentialId,
            credentials.GetValueOrDefault(c.CredentialId, "Unknown"),
            c.Platform,
            c.IsEnabled,
            c.PlatformConfig,
            c.CreatedAt,
            c.UpdatedAt));

        return TypedResults.Ok(response);
    }

    private static ErrorResponse CreateError(string code, string message) =>
        new() { Errors = [new Error { Code = code, Message = message }] };
}