using Orchitect.Core.Domain.Organisation;

namespace Orchitect.Engine.Domain.Service;

/// <summary>
/// Represents a Service (e.g., API Service) that can contain one or more Applications.
/// Each Service has an assigned Owner.
/// </summary>
public sealed record Service
{
    public required ServiceId Id { get; init; }
    public required OrganisationId OrganisationId { get; init; }
    public required string Name { get; init; }
    public required DateTime CreatedAt { get; init; }
    public required DateTime UpdatedAt { get; init; }

    private Service()
    {
    }

    public static Service Create(string name, OrganisationId organisationId)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);

        return new Service
        {
            Id = new ServiceId(),
            OrganisationId = organisationId,
            Name = name,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }
}