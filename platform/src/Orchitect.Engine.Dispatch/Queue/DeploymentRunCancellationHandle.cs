namespace Orchitect.Engine.Dispatch.Queue;

public sealed class DeploymentRunCancellationHandle : IDisposable
{
    private readonly CancellationTokenSource _source;
    private readonly Action _release;

    internal DeploymentRunCancellationHandle(CancellationTokenSource source, Action release)
    {
        _source = source;
        _release = release;
    }

    public CancellationToken StopRequested => _source.Token;

    public void Dispose()
    {
        _release();
        _source.Dispose();
    }
}
