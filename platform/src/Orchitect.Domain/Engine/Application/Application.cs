using Orchitect.Domain.Core;
using Orchitect.Domain.Core.Organisation;
using Orchitect.Domain.Engine.Git;

namespace Orchitect.Domain.Engine.Application;

/// <summary>
/// Represents an Application that encompasses the Git Repository,
/// Pipelines, required Resources, and deployed environments for an Application  
/// </summary>
public sealed record Application : IEntity
{
    public required ApplicationId Id { get; init; }
    public required OrganisationId OrganisationId { get; init; }
    public required string Name { get; init; }
    public required Repository Repository { get; init; }
    public required DateTime CreatedAt { get; init; }
    public required DateTime UpdatedAt { get; init; }

    private Application()
    {
    }

    public static Application Create(string name, Repository repository, OrganisationId organisationId)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        ThrowIfInvalid(repository);

        return new Application
        {
            Id = new ApplicationId(),
            OrganisationId = organisationId,
            Name = name,
            Repository = repository,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }

    public static Application Create(CreateApplicationRequest request)
    {
        var repository = new Repository
        {
            Name = request.Repository.Name,
            Url = request.Repository.Url,
            Provider = request.Repository.Provider
        };

        return Create(request.Name, repository, new OrganisationId(Guid.Parse(request.OrganisationId)));
    }

    public Application Update(string name, Repository repository)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        ThrowIfInvalid(repository);

        return this with
        {
            Name = name,
            Repository = repository,
            UpdatedAt = DateTime.UtcNow
        };
    }

    private static void ThrowIfInvalid(Repository repository)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentException.ThrowIfNullOrEmpty(repository.Name);

        if (!GitValidator.IsValidRepositoryUrl(repository.Url))
        {
            throw new ArgumentException($"Repository url '{repository.Url}' is not a valid git repository url.",
                nameof(repository));
        }
    }
}