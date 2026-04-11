using Microsoft.EntityFrameworkCore;
using Orchitect.Domain.Core.Organisation;
using Orchitect.Domain.Inventory.SourceControl;
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

    public async Task<IReadOnlyList<PullRequest>> GetByOrganisationIdAsync(
        OrganisationId organisationId,
        CancellationToken cancellationToken = default)
    {
        return await _context.PullRequests
            .Where(pr => pr.OrganisationId == organisationId)
            .OrderBy(pr => pr.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<PullRequest>> GetByRepositoryAsync(
        Uri repositoryUrl,
        CancellationToken cancellationToken = default)
    {
        return await _context.PullRequests
            .Where(pr => pr.RepositoryUrl == repositoryUrl)
            .OrderByDescending(pr => pr.CreatedOnDate)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<PullRequest>> GetActiveAsync(
        OrganisationId organisationId,
        CancellationToken cancellationToken = default)
    {
        return await _context.PullRequests
            .Where(pr => pr.OrganisationId == organisationId &&
                         (pr.Status == PullRequestStatus.Active || pr.Status == PullRequestStatus.Draft))
            .OrderByDescending(pr => pr.CreatedOnDate)
            .ToListAsync(cancellationToken);
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