namespace Conductor.Engine.Domain.Organisation;

/// <summary>
/// Represents a Team that can be a part of an organisation.
/// </summary>
public sealed record OrganisationTeam
{
    public required OrganisationTeamId Id { get; init; }
    public required OrganisationId OrganisationId { get; init; }
    public required string Name { get; init; }
    public required DateTime CreatedAt { get; init; }
    public required DateTime UpdatedAt { get; init; }

    private OrganisationTeam()
    {
    }

    public static OrganisationTeam Create(string name, OrganisationId organisationId)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);

        return new OrganisationTeam
        {
            Id = new OrganisationTeamId(),
            OrganisationId = organisationId,
            Name = name,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
    }
}