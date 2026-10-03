using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using Orchitect.Api.Shared;
using Orchitect.Api.Shared.Authorization;
using Orchitect.Domain.Engine.ResourceTemplate;

namespace Orchitect.Api.Endpoints.Engine.ResourceTemplate;

public sealed class GetAllResourceTemplatesEndpoint : IEndpoint
{
    public static void Map(IEndpointRouteBuilder builder) => builder
        .MapGet("/", HandleAsync)
        .HandlesOrganisationScope()
        .WithSummary("Gets all resource templates.");

    public sealed record GetAllResourceTemplatesResponse(
        List<GetResourceTemplateEndpoint.GetResourceTemplateResponse> ResourceTemplates);

    private static async Task<Results<Ok<GetAllResourceTemplatesResponse>, NotFound, InternalServerError>> HandleAsync(
        [FromServices]
        IResourceTemplateRepository repository,
        [FromServices]
        IOrganisationAccess organisationAccess,
        [FromServices]
        ILogger<GetResourceTemplateEndpoint> logger,
        CancellationToken cancellationToken)
    {
        try
        {
            var organisationIds = await organisationAccess.GetOrganisationIdsAsync(cancellationToken);
            var resourceTemplates = await repository.GetByOrganisationIdsAsync(organisationIds, cancellationToken);
            var resourceTemplatesResponse = resourceTemplates
                .Select(r =>
                {
                    var versions = r.Versions.Select(v => new GetResourceTemplateEndpoint.ResourceTemplateVersionResponse(
                        Id: v.Id.Value,
                        Version: v.Version,
                        Source: new GetResourceTemplateEndpoint.ResourceTemplateVersionSourceResponse(
                            BaseUrl: v.Source.BaseUrl,
                            FolderPath: v.Source.FolderPath,
                            Tag: v.Source.Tag),
                        Notes: v.Notes,
                        State: v.State,
                        CreatedAt: v.CreatedAt
                    )).ToList();

                    return new GetResourceTemplateEndpoint.GetResourceTemplateResponse(
                        Id: r.Id.Value,
                        OrganisationId: r.OrganisationId.Value,
                        Name: r.Name,
                        Type: r.Type,
                        Description: r.Description,
                        Provider: r.Provider,
                        CreatedAt: r.CreatedAt,
                        UpdatedAt: r.UpdatedAt,
                        Versions: versions
                    );
                })
                .ToList();

            return TypedResults.Ok(new GetAllResourceTemplatesResponse(resourceTemplatesResponse));
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Could not retrieve All Resource Templates");
            return TypedResults.InternalServerError();
        }
    }
}