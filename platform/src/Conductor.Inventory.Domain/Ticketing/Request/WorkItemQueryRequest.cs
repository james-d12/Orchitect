namespace Conductor.Inventory.Domain.Ticketing.Request;

public sealed record WorkItemQueryRequest(
    string? Id,
    string? Title);