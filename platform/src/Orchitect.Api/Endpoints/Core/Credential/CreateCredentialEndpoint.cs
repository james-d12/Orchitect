using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Orchitect.Api.Shared;
using Orchitect.Domain.Core.Credential;
using Orchitect.Domain.Core.Organisation;

namespace Orchitect.Api.Endpoints.Core.Credential;

public sealed class CreateCredentialEndpoint : IEndpoint
{
    public static void Map(IEndpointRouteBuilder builder) => builder
        .MapPost("/", HandleAsync)
        .WithSummary("Creates a new credential.");

    private static async Task<Results<Ok<CredentialResponse>, InternalServerError>> HandleAsync(
        [FromBody]
        CreateCredentialRequest request,
        [FromServices]
        ICredentialRepository repository,
        [FromServices]
        IEncryptionService encryptionService,
        CancellationToken cancellationToken)
    {
        var organisationId = new OrganisationId(request.OrganisationId);
        var payloadJson = request.Payload.GetRawText();
        var encryptedPayload = encryptionService.Encrypt(payloadJson);

        var credential = Domain.Core.Credential.Credential.Create(
            organisationId,
            request.Name,
            request.Type,
            request.Platform,
            encryptedPayload);

        var result = await repository.CreateAsync(credential, cancellationToken);

        if (result is null)
        {
            return TypedResults.InternalServerError();
        }

        return TypedResults.Ok(CredentialResponse.From(result));
    }
}