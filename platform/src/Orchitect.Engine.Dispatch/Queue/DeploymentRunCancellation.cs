using System.Collections.Concurrent;
using Orchitect.Domain.Engine.Deployment;

namespace Orchitect.Engine.Dispatch.Queue;

public sealed class DeploymentRunCancellation : IDeploymentRunCancellation
{
    private readonly ConcurrentDictionary<DeploymentRunId, CancellationTokenSource> _runs = new();

    public DeploymentRunCancellationHandle Track(DeploymentRunId runId)
    {
        var source = new CancellationTokenSource();

        if (!_runs.TryAdd(runId, source))
        {
            source.Dispose();
            throw new InvalidOperationException($"Run '{runId.Value}' is already being tracked.");
        }

        return new DeploymentRunCancellationHandle(source,
            () => _runs.TryRemove(new KeyValuePair<DeploymentRunId, CancellationTokenSource>(runId, source)));
    }

    public bool RequestStop(DeploymentRunId runId)
    {
        if (!_runs.TryGetValue(runId, out var source))
        {
            return false;
        }

        try
        {
            source.Cancel();
            return true;
        }
        catch (ObjectDisposedException)
        {
            return false;
        }
    }
}
