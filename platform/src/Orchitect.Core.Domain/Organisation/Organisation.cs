namespace Orchitect.Core.Domain.Organisation;

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
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }

    public Organisation Update(string name)
    {
        ArgumentNullException.ThrowIfNull(name);

        return this with
        {
            Name = name,
            UpdatedAt = DateTime.UtcNow
        };
    }

    public Organisation AddUser(string identityUserId)
    {
        ArgumentException.ThrowIfNullOrEmpty(identityUserId);

        if (Users.Any(u => u.IdentityUserId == identityUserId))
        {
            throw new InvalidOperationException($"User with IdentityUserId '{identityUserId}' already exists in this organisation.");
        }

        var newUser = OrganisationUser.Create(identityUserId, Id);
        var updatedUsers = new List<OrganisationUser>(Users) { newUser };

        return this with
        {
            Users = updatedUsers,
            UpdatedAt = DateTime.UtcNow
        };
    }

    public Organisation RemoveUser(OrganisationUserId userId)
    {
        var updatedUsers = Users.Where(u => u.Id != userId).ToList();
        var updatedTeams = Teams.Select(t => t.RemoveUser(userId)).ToList();

        return this with
        {
            Users = updatedUsers,
            Teams = updatedTeams,
            UpdatedAt = DateTime.UtcNow
        };
    }

    public Organisation AddTeam(string name)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);

        if (Teams.Any(t => t.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException($"Team with name '{name}' already exists in this organisation.");
        }

        var newTeam = OrganisationTeam.Create(name, Id);
        var updatedTeams = new List<OrganisationTeam>(Teams) { newTeam };

        return this with
        {
            Teams = updatedTeams,
            UpdatedAt = DateTime.UtcNow
        };
    }

    public Organisation RemoveTeam(OrganisationTeamId teamId)
    {
        var updatedTeams = Teams.Where(t => t.Id != teamId).ToList();

        return this with
        {
            Teams = updatedTeams,
            UpdatedAt = DateTime.UtcNow
        };
    }

    public Organisation AddUserToTeam(OrganisationUserId userId, OrganisationTeamId teamId)
    {
        if (Users.All(u => u.Id != userId))
        {
            throw new InvalidOperationException($"User with Id '{userId}' does not exist in this organisation.");
        }

        var team = Teams.FirstOrDefault(t => t.Id == teamId);
        if (team is null)
        {
            throw new InvalidOperationException($"Team with Id '{teamId}' does not exist in this organisation.");
        }

        var updatedTeam = team.AddUser(userId);
        var updatedTeams = Teams.Select(t => t.Id == teamId ? updatedTeam : t).ToList();

        return this with
        {
            Teams = updatedTeams,
            UpdatedAt = DateTime.UtcNow
        };
    }

    public Organisation RemoveUserFromTeam(OrganisationUserId userId, OrganisationTeamId teamId)
    {
        var team = Teams.FirstOrDefault(t => t.Id == teamId);
        if (team is null)
        {
            throw new InvalidOperationException($"Team with Id '{teamId}' does not exist in this organisation.");
        }

        var updatedTeam = team.RemoveUser(userId);
        var updatedTeams = Teams.Select(t => t.Id == teamId ? updatedTeam : t).ToList();

        return this with
        {
            Teams = updatedTeams,
            UpdatedAt = DateTime.UtcNow
        };
    }
}