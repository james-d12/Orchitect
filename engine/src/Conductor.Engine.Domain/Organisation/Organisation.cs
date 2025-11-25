namespace Conductor.Engine.Domain.Organisation;

public sealed record Organisation
{
    public required OrganisationId Id { get; init; }
    public required string Name { get; init; } = string.Empty;
    public required List<OrganisationUser> Users { get; init; }
    public required List<OrganisationTeam> Teams { get; init; }
    public required DateTime CreatedAt { get; init; }
    public required DateTime UpdatedAt { get; init; }

    private Organisation()
    {
    }

    public static Organisation Create(string name)
    {
        ArgumentNullException.ThrowIfNull(name);

        return new Organisation
        {
            Id = new OrganisationId(),
            Name = name,
            Users = [],
            Teams = [],
            CreatedAt = DateTime.Now,
            UpdatedAt = DateTime.Now
        };
    }

    public Organisation Update(string name)
    {
        ArgumentNullException.ThrowIfNull(name);

        return this with
        {
            Name = name,
            UpdatedAt = DateTime.Now
        };
    }
}