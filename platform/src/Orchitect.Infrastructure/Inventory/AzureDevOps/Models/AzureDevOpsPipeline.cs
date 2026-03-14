using Orchitect.Domain.Inventory.Git;

namespace Orchitect.Infrastructure.Inventory.AzureDevOps.Models;

public sealed record AzureDevOpsPipeline : Pipeline
{
    public required string Path { get; init; }
}