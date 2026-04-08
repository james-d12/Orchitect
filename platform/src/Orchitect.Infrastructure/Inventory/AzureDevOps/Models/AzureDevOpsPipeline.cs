using Orchitect.Domain.Inventory.Pipeline;

namespace Orchitect.Infrastructure.Inventory.AzureDevOps.Models;

public sealed record AzureDevOpsPipeline : Pipeline
{
    public required string Path { get; init; }
}