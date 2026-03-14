namespace Orchitect.Domain.Engine.Application;

public sealed record CreateApplicationRequest(
    string Name,
    string OrganisationId,
    CreateRepositoryRequest Repository
);