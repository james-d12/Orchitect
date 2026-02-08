using Orchitect.Inventory.Domain.Git;

namespace Orchitect.Inventory.Infrastructure.AzureDevOps.Models;

public sealed record AzureDevOpsPipeline : Pipeline
{
    public required string Path { get; init; }
}