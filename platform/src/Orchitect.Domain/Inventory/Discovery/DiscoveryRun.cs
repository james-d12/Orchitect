namespace Orchitect.Domain.Inventory.Discovery;

public sealed record DiscoveryRun
{
    public required DiscoveryRunId Id { get; init; }
    public required DiscoveryConfigurationId DiscoveryConfigurationId { get; init; }
    public required DiscoveryRunStatus Status { get; init; }
    public required DateTime StartedAt { get; init; }
    public DateTime? CompletedAt { get; init; }
    public DiscoveryCounts Counts { get; init; } = new();
    public string? ErrorMessage { get; init; }

    public const int ErrorMessageMaxLength = 2000;

    private DiscoveryRun()
    {
    }

    public static DiscoveryRun Start(DiscoveryConfigurationId configurationId)
    {
        return new DiscoveryRun
        {
            Id = new DiscoveryRunId(),
            DiscoveryConfigurationId = configurationId,
            Status = DiscoveryRunStatus.Running,
            StartedAt = DateTime.UtcNow
        };
    }

    public DiscoveryRun Succeed(DiscoveryCounts counts)
    {
        EnsureRunning();

        return this with
        {
            Status = DiscoveryRunStatus.Succeeded,
            CompletedAt = DateTime.UtcNow,
            Counts = counts
        };
    }

    public DiscoveryRun Fail(string errorMessage)
    {
        EnsureRunning();
        ArgumentException.ThrowIfNullOrWhiteSpace(errorMessage);

        return this with
        {
            Status = DiscoveryRunStatus.Failed,
            CompletedAt = DateTime.UtcNow,
            ErrorMessage = errorMessage.Length > ErrorMessageMaxLength
                ? errorMessage[..ErrorMessageMaxLength]
                : errorMessage
        };
    }

    private void EnsureRunning()
    {
        if (Status != DiscoveryRunStatus.Running)
        {
            throw new InvalidOperationException($"Discovery run '{Id.Value}' has already finished as {Status}.");
        }
    }
}
