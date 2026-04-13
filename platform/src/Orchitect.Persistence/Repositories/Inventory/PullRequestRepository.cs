using Microsoft.EntityFrameworkCore;
using Orchitect.Common.Query;
using Orchitect.Domain.Inventory.SourceControl;
using Orchitect.Domain.Inventory.SourceControl.Requests;
using Orchitect.Domain.Inventory.SourceControl.Services;

namespace Orchitect.Persistence.Repositories.Inventory;

public sealed class PullRequestRepository : IPullRequestRepository
{
    private readonly OrchitectDbContext _context;

    public PullRequestRepository(OrchitectDbContext context)
    {
        _context = context;
    }

    public IEnumerable<PullRequest> GetAll()
    {
        return _context.PullRequests
            .OrderBy(pr => pr.OrganisationId)
            .ThenBy(pr => pr.Name)
            .ToList();
    }

    public async Task<PullRequest?> GetByIdAsync(
        PullRequestId id,
        CancellationToken cancellationToken = default)
    {
        return await _context.PullRequests
            .AsNoTracking()
            .FirstOrDefaultAsync(pr => pr.Id == id, cancellationToken);
    }

    public IReadOnlyList<PullRequest> GetByQuery(PullRequestQuery query)
    {
        var pullRequests = GetAll();

        return new QueryBuilder<PullRequest>(pullRequests)
            .Where(query.Id, p => p.Id.Value == query.Id)
            .Where(query.Name, p => p.Name.Contains(query.Name ?? string.Empty))
            .Where(query.Description, p => p.Description.Contains(query.Description ?? string.Empty))
            .Where(query.Url, p => p.Url.ToString().Contains(query.Url ?? string.Empty))
            .Where(query.Labels, p => query.Labels!.Any(l => p.Labels.Contains(l)))
            .Where(query.Platform, p => p.Platform == query.Platform)
            .ToList();
    }

    public async Task<PullRequest?> CreateAsync(
        PullRequest pullRequest,
        CancellationToken cancellationToken = default)
    {
        await _context.PullRequests.AddAsync(pullRequest, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        return pullRequest;
    }

    public async Task BulkUpsertAsync(
        IEnumerable<PullRequest> pullRequests,
        CancellationToken cancellationToken = default)
    {
        foreach (var pullRequest in pullRequests)
        {
            var existing = await _context.PullRequests
                .FirstOrDefaultAsync(pr => pr.Url == pullRequest.Url, cancellationToken);

            if (existing is null)
            {
                await _context.PullRequests.AddAsync(pullRequest, cancellationToken);
            }
            else
            {
                _context.Entry(existing).CurrentValues.SetValues(pullRequest);
            }
        }

        await _context.SaveChangesAsync(cancellationToken);
    }
}
