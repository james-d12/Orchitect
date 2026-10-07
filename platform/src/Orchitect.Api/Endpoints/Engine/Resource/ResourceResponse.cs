namespace Orchitect.Api.Endpoints.Engine.Resource;

public sealed record ResourceResponse(
    Guid Id,
    Guid OrganisationId,
    string Name,
    string Slug,
    string Description,
    string Kind,
    Guid ResourceTemplateId,
    Guid EnvironmentId,
    Guid? ApplicationId,
    List<Guid> Consumers,
    DateTime CreatedAt,
    DateTime UpdatedAt)
{
    public static ResourceResponse From(Orchitect.Domain.Engine.Resource.Resource resource) => new(
        resource.Id.Value,
        resource.OrganisationId.Value,
        resource.Name,
        resource.Slug,
        resource.Description,
        resource.Kind.ToString(),
        resource.ResourceTemplateId.Value,
        resource.EnvironmentId.Value,
        resource.ApplicationId?.Value,
        resource.Consumers.Select(c => c.Value).ToList(),
        resource.CreatedAt,
        resource.UpdatedAt);
}
