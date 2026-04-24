using Microsoft.EntityFrameworkCore;
using Orchitect.Common.Query;
using Orchitect.Domain.Inventory.Issue;
using Orchitect.Domain.Inventory.Issue.Requests;
using Orchitect.Domain.Inventory.Issue.Services;

namespace Orchitect.Persistence.Repositories.Inventory;

public sealed class IssueRepository : IIssueRepository
{
    private readonly OrchitectDbContext _context;

    public IssueRepository(OrchitectDbContext context)
    {
        _context = context;
    }

    public IEnumerable<Issue> GetAll()
    {
        return _context.Issues
            .OrderBy(wi => wi.OrganisationId)
            .ThenBy(wi => wi.Title)
            .ToList();
    }

    public async Task<Issue?> GetByIdAsync(
        IssueId id,
        CancellationToken cancellationToken = default)
    {
        return await _context.Issues
            .AsNoTracking()
            .FirstOrDefaultAsync(wi => wi.Id == id, cancellationToken);
    }

    public IReadOnlyList<Issue> GetByQuery(IssueQuery query)
    {
        var issues = GetAll().Where(i => i.OrganisationId == query.OrganisationId);

        return new QueryBuilder<Issue>(issues)
            .Where(query.Id, p => p.Id.Value == query.Id)
            .Where(query.Title, p => p.Title.Contains(query.Title ?? string.Empty))
            .ToList();
    }

    public async Task<Issue?> CreateAsync(
        Issue issue,
        CancellationToken cancellationToken = default)
    {
        await _context.Issues.AddAsync(issue, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        return issue;
    }

    public async Task BulkUpsertAsync(
        IEnumerable<Issue> workItems,
        CancellationToken cancellationToken = default)
    {
        foreach (var workItem in workItems)
        {
            var existing = await _context.Issues
                .FirstOrDefaultAsync(wi => wi.Url == workItem.Url, cancellationToken);

            if (existing is null)
            {
                await _context.Issues.AddAsync(workItem, cancellationToken);
            }
            else
            {
                _context.Entry(existing).CurrentValues.SetValues(workItem);
            }
        }

        await _context.SaveChangesAsync(cancellationToken);
    }
}