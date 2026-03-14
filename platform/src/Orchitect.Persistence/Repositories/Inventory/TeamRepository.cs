using Microsoft.EntityFrameworkCore;
using Orchitect.Domain.Core.Organisation;
using Orchitect.Domain.Inventory.Shared;
using Orchitect.Domain.Inventory.Shared.Service;

namespace Orchitect.Persistence.Repositories.Inventory;

public sealed class TeamRepository : ITeamRepository
{
    private readonly OrchitectDbContext _context;

    public TeamRepository(OrchitectDbContext context)
    {
        _context = context;
    }

    public IEnumerable<Team> GetAll()
    {
        return _context.Teams
            .OrderBy(t => t.OrganisationId)
            .ThenBy(t => t.Name)
            .ToList();
    }

    public async Task<Team?> GetByIdAsync(
        TeamId id,
        CancellationToken cancellationToken = default)
    {
        return await _context.Teams
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<Team>> GetByOrganisationIdAsync(
        OrganisationId organisationId,
        CancellationToken cancellationToken = default)
    {
        return await _context.Teams
            .Where(t => t.OrganisationId == organisationId)
            .OrderBy(t => t.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Team>> GetByPlatformAsync(
        OrganisationId organisationId,
        TeamPlatform platform,
        CancellationToken cancellationToken = default)
    {
        return await _context.Teams
            .Where(t => t.OrganisationId == organisationId && t.Platform == platform)
            .OrderBy(t => t.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<Team?> GetByUrlAsync(
        string url,
        CancellationToken cancellationToken = default)
    {
        return await _context.Teams
            .FirstOrDefaultAsync(t => t.Url.ToString() == url, cancellationToken);
    }

    public async Task<Team?> CreateAsync(
        Team team,
        CancellationToken cancellationToken = default)
    {
        await _context.Teams.AddAsync(team, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        return team;
    }

    public async Task BulkUpsertAsync(
        IEnumerable<Team> teams,
        CancellationToken cancellationToken = default)
    {
        foreach (var team in teams)
        {
            var existing = await _context.Teams
                .FirstOrDefaultAsync(t => t.Url == team.Url, cancellationToken);

            if (existing is null)
            {
                await _context.Teams.AddAsync(team, cancellationToken);
            }
            else
            {
                _context.Entry(existing).CurrentValues.SetValues(team);
            }
        }

        await _context.SaveChangesAsync(cancellationToken);
    }
}
