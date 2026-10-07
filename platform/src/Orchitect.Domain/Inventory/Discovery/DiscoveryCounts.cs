namespace Orchitect.Domain.Inventory.Discovery;

public sealed record DiscoveryCounts
{
    public int Teams { get; init; }
    public int Repositories { get; init; }
    public int Pipelines { get; init; }
    public int PullRequests { get; init; }
    public int Issues { get; init; }
    public int CloudResources { get; init; }
    public int CloudSecrets { get; init; }

    public int Total => Teams + Repositories + Pipelines + PullRequests + Issues + CloudResources + CloudSecrets;
}
