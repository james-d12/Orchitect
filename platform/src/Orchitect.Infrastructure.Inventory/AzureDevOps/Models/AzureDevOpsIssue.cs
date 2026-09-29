using System.Collections.Frozen;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using Orchitect.Domain.Inventory.Issue;

namespace Orchitect.Infrastructure.Inventory.AzureDevOps.Models;

[ExcludeFromCodeCoverage]
public sealed record AzureDevOpsIssue : Issue
{
    public required int Revision { get; init; }
    public required FrozenDictionary<string, object> Fields { get; init; }
    public required ImmutableHashSet<string> Relations { get; init; }
}