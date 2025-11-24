namespace Conductor.Engine.Domain.Organisation;

/// <summary>
/// Represents a Service (e.g., API Service) that can contain one or more Applications.
/// Each Service has an assigned Owner.
/// </summary>
public sealed record OrganisationService
{
    public required OrganisationServiceId Id { get; init; }
    public required OrganisationId OrganisationId { get; init; }
    public required string Name { get; init; }
    public required DateTime CreatedAt { get; init; }
    public required DateTime UpdatedAt { get; init; }

    private OrganisationService()
    {
    }

    public static OrganisationService Create(string name, OrganisationId organisationId)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);

        return new OrganisationService
        {
            Id = new OrganisationServiceId(),
            OrganisationId = organisationId,
            Name = name,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }
}