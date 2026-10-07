using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Orchitect.Api.Jobs;
using Orchitect.Api.Shared;
using Orchitect.Api.Shared.Authorization;
using Orchitect.Domain.Core.Credential;
using Orchitect.Domain.Core.Organisation;
using Orchitect.Domain.Inventory.Discovery;
using Orchitect.Domain.Inventory.Discovery.Services;

namespace Orchitect.Api.Endpoints.Inventory.Discovery;

public sealed class TriggerDiscoveryEndpoint : IEndpoint
{
    public sealed record TriggerDiscoveryResponse(Guid RunId);

    public static void Map(IEndpointRouteBuilder builder) => builder
        .MapPost("/{id}/trigger", HandleAsync)
        .RequireOrganisationMember()
        .WithName("TriggerDiscovery")
        .WithSummary("Manually trigger discovery for a specific configuration")
        .Produces<TriggerDiscoveryResponse>(StatusCodes.Status202Accepted)
        .Produces<ErrorResponse>(StatusCodes.Status404NotFound)
        .Produces<ErrorResponse>(StatusCodes.Status400BadRequest);

    private static async Task<Results<Accepted<TriggerDiscoveryResponse>, NotFound<ErrorResponse>, BadRequest<ErrorResponse>>> HandleAsync(
        [FromRoute]
        Guid id,
        [FromQuery]
        Guid organisationId,
        [FromServices]
        IDiscoveryConfigurationRepository configRepository,
        [FromServices]
        ICredentialRepository credentialRepository,
        [FromServices]
        DiscoveryRunner runner,
        [FromServices]
        IServiceScopeFactory scopeFactory,
        CancellationToken cancellationToken)
    {
        var orgId = new OrganisationId(organisationId);
        var configId = new DiscoveryConfigurationId(id);

        var config = await configRepository.GetByIdAsync(configId, cancellationToken);
        if (config == null || config.OrganisationId != orgId)
            return TypedResults.NotFound(CreateError("CONFIG_NOT_FOUND", "Discovery configuration not found"));

        var credential = await credentialRepository.GetByIdAsync(config.CredentialId, cancellationToken);
        if (credential == null)
            return TypedResults.BadRequest(CreateError("CREDENTIAL_NOT_FOUND", "Associated credential not found"));

        var run = await runner.StartAsync(config, cancellationToken);

        _ = Task.Run(async () =>
        {
            using var scope = scopeFactory.CreateScope();
            var scopedRunner = scope.ServiceProvider.GetRequiredService<DiscoveryRunner>();
            await scopedRunner.ExecuteAsync(config, run, CancellationToken.None);
        }, CancellationToken.None);

        return TypedResults.Accepted($"/discovery/{id}/status", new TriggerDiscoveryResponse(run.Id.Value));
    }

    private static ErrorResponse CreateError(string code, string message) =>
        new() { Errors = [new Error { Code = code, Message = message }] };
}
