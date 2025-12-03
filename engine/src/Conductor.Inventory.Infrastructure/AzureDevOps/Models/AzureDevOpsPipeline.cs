using Conductor.Inventory.Domain.Git;

namespace Conductor.Inventory.Infrastructure.AzureDevOps.Models;

public sealed record AzureDevOpsPipeline : Pipeline
{
    public required string Path { get; init; }
}