using Orchitect.Domain.Engine.Deployment;
using Orchitect.Engine.Dispatch.Queue;

namespace Orchitect.Engine.Dispatch.Unit.Tests.Queue;

public sealed class DeploymentRunCancellationTests
{
    private readonly DeploymentRunCancellation _cancellation = new();

    [Fact]
    public void RequestStop_TrackedRun_SignalsItsToken()
    {
        var runId = new DeploymentRunId();
        using var handle = _cancellation.Track(runId);

        var stopped = _cancellation.RequestStop(runId);

        Assert.True(stopped);
        Assert.True(handle.StopRequested.IsCancellationRequested);
    }

    [Fact]
    public void RequestStop_UntrackedRun_ReturnsFalse()
    {
        Assert.False(_cancellation.RequestStop(new DeploymentRunId()));
    }

    [Fact]
    public void RequestStop_AfterHandleDisposed_ReturnsFalse()
    {
        var runId = new DeploymentRunId();
        _cancellation.Track(runId).Dispose();

        Assert.False(_cancellation.RequestStop(runId));
    }

    [Fact]
    public void RequestStop_OnlySignalsTheRequestedRun()
    {
        var runId = new DeploymentRunId();
        using var handle = _cancellation.Track(runId);
        using var other = _cancellation.Track(new DeploymentRunId());

        _cancellation.RequestStop(runId);

        Assert.False(other.StopRequested.IsCancellationRequested);
    }

    [Fact]
    public void Track_RunAlreadyTracked_Throws()
    {
        var runId = new DeploymentRunId();
        using var handle = _cancellation.Track(runId);

        Assert.Throws<InvalidOperationException>(() => _cancellation.Track(runId));
    }
}
