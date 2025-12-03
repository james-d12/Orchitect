using System.Collections.Frozen;
using System.Collections.Immutable;
using Conductor.Inventory.Domain.Ticketing;

namespace Conductor.Inventory.Infrastructure.AzureDevOps.Models;

public sealed record AzureDevOpsWorkItem : WorkItem
{
    public required int Revision { get; init; }
    public required FrozenDictionary<string, object> Fields { get; init; }
    public required ImmutableHashSet<string> Relations { get; init; }
}