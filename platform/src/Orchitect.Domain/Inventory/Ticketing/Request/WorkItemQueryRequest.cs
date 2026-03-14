namespace Orchitect.Domain.Inventory.Ticketing.Request;

public sealed record WorkItemQueryRequest(
    string? Id,
    string? Title);