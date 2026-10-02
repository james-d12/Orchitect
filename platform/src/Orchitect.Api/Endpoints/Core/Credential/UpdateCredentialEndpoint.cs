using System.Text.Json;
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

public sealed class UpdateCredentialEndpoint : IEndpoint
{
    public static void Map(IEndpointRouteBuilder builder) => builder
        .MapPut("/{id:guid}", HandleAsync)
        .WithSummary("Updates an existing credential.");

    public sealed record UpdateCredentialRequest(
        string Name,
        CredentialType Type,
        CredentialPlatform Platform,
        JsonElement Payload);

    private static async Task<Results<Ok<CredentialResponse>, NotFound, InternalServerError>> HandleAsync(
        [FromRoute]
        Guid id,
        [FromBody]
        UpdateCredentialRequest request,
        [FromServices]
        ICredentialRepository repository,
        [FromServices]
        IOrganisationRepository organisationRepository,
        ClaimsPrincipal user,
        [FromServices]
        IEncryptionService encryptionService,
        CancellationToken cancellationToken)
    {
        var credentialId = new CredentialId(id);
        var existing = await repository.GetByIdAsync(credentialId, cancellationToken);

        if (existing is null ||
            !await organisationRepository.IsMemberAsync(user, existing.OrganisationId, cancellationToken))
        {
            return TypedResults.NotFound();
        }

        var payloadJson = request.Payload.GetRawText();
        var encryptedPayload = encryptionService.Encrypt(payloadJson);
        var updated = existing.Update(request.Name, request.Type, request.Platform, encryptedPayload);
        var result = await repository.UpdateAsync(updated, cancellationToken);

        if (result is null)
        {
            return TypedResults.InternalServerError();
        }

        return TypedResults.Ok(CredentialResponse.From(result));
    }
}