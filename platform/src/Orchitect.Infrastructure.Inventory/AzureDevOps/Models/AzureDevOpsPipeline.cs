using System.Diagnostics.CodeAnalysis;
using Orchitect.Domain.Inventory.Pipeline;

namespace Orchitect.Infrastructure.Inventory.AzureDevOps.Models;

[ExcludeFromCodeCoverage]
public sealed record AzureDevOpsPipeline : Pipeline
{
    public required string Path { get; init; }
}