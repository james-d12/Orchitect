namespace Orchitect.Engine.Dispatch.Queue;

public interface IDeploymentQueue
{
    public Task QueueDeploymentTaskAsync(DeploymentQueueRequest request, CancellationToken token = default);
}