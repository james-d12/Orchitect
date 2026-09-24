namespace Orchitect.Infrastructure.Engine.Queue;

public interface IDeploymentQueue
{
    public Task QueueDeploymentTaskAsync(DeploymentQueueRequest request, CancellationToken token = default);
}