namespace Orchitect.Engine.Domain.Organisation;

/// <summary>
/// Represents a Team that can be a part of an organisation.
/// </summary>
public sealed record OrganisationTeam
{
    public required OrganisationTeamId Id { get; init; }
    public required OrganisationId OrganisationId { get; init; }
    public required string Name { get; init; }
    public required List<OrganisationUserId> UserIds { get; init; }
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
            UserIds = [],
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
    }

    public OrganisationTeam AddUser(OrganisationUserId userId)
    {
        if (UserIds.Contains(userId))
        {
            return this;
        }

        var updatedUserIds = new List<OrganisationUserId>(UserIds) { userId };

        return this with
        {
            UserIds = updatedUserIds,
            UpdatedAt = DateTime.UtcNow
        };
    }

    public OrganisationTeam RemoveUser(OrganisationUserId userId)
    {
        var updatedUserIds = UserIds.Where(id => id != userId).ToList();

        return this with
        {
            UserIds = updatedUserIds,
            UpdatedAt = DateTime.UtcNow
        };
    }
}