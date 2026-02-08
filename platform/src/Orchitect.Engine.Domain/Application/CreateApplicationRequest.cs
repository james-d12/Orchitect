namespace Orchitect.Engine.Domain.Application;

public sealed record CreateApplicationRequest(
    string Name,
    string OrganisationId,
    CreateRepositoryRequest Repository
);