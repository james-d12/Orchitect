using Orchitect.Domain.Core.Organisation;
using Orchitect.Domain.Inventory.Shared;

namespace Orchitect.Domain.Inventory.Issue.Requests;

public sealed record IssueQuery(
    OrganisationId OrganisationId,
    string? Id = null,
    string? Title = null) : BaseQuery(OrganisationId);