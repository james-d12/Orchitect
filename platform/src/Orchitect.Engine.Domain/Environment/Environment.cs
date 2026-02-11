using Orchitect.Core.Domain.Organisation;

namespace Orchitect.Engine.Domain.Environment;

/// <summary>
/// Represents a deployment environment. E.g. a Kubernetes Cluster, an ArgoCD Environment, etc.
/// E.g DEV, UAT, PROD
/// </summary>
public sealed record Environment
{
    public required EnvironmentId Id { get; init; }
    public required OrganisationId OrganisationId { get; init; }
    public required string Name { get; init; }
    public required string Description { get; init; }
    public required DateTime CreatedAt { get; init; }
    public required DateTime UpdatedAt { get; init; }

    private Environment()
    {
    }

    public static Environment Create(string name, string description, OrganisationId organisationId)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(description);

        return new Environment
        {
            Id = new EnvironmentId(),
            OrganisationId = organisationId,
            Name = name,
            Description = description,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }

    public static Environment Create(CreateEnvironmentRequest request)
    {
        return Create(request.Name, request.Description, request.OrganisationId);
    }

    public Environment Update(string name, string description)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(description);

        return this with
        {
            Name = name,
            Description = description,
            UpdatedAt = DateTime.UtcNow
        };
    }
}