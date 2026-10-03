using Orchitect.Domain.Engine.Deployment;
using Orchitect.Domain.Engine.Environment;
using ApplicationId = Orchitect.Domain.Engine.Application.ApplicationId;

namespace Orchitect.Domain.Unit.Tests.Engine;

public sealed class DeploymentTests
{
    [Fact]
    public void Start_Pending_BecomesDeploying()
    {
        var deployment = NewDeployment();

        var started = deployment.Start();

        Assert.Equal(DeploymentStatus.Deploying, started.Status);
        Assert.Equal(DeploymentStatus.Pending, deployment.Status);
        Assert.True(started.UpdatedAt >= deployment.UpdatedAt);
    }

    [Fact]
    public void Start_AlreadyStarted_Throws()
    {
        Assert.Throws<InvalidOperationException>(() => Deploying().Start());
    }

    [Theory]
    [InlineData(0, DeploymentStatus.Deployed)]
    [InlineData(1, DeploymentStatus.Failed)]
    [InlineData(137, DeploymentStatus.Failed)]
    [InlineData(-1, DeploymentStatus.Failed)]
    public void ProcessDeploymentStatus_ExitCode_DecidesStatus(long exitCode, DeploymentStatus expected)
    {
        var processed = Deploying().ProcessDeploymentStatus(exitCode, null);

        Assert.Equal(expected, processed.Status);
    }

    [Fact]
    public void ProcessDeploymentStatus_Exception_Fails()
    {
        var processed = Deploying().ProcessDeploymentStatus(null, new InvalidOperationException("image missing"));

        Assert.Equal(DeploymentStatus.Failed, processed.Status);
    }

    [Fact]
    public void ProcessDeploymentStatus_ExceptionWithZeroExitCode_Fails()
    {
        var processed = Deploying().ProcessDeploymentStatus(0, new InvalidOperationException("boom"));

        Assert.Equal(DeploymentStatus.Failed, processed.Status);
    }

    [Fact]
    public void ProcessDeploymentStatus_TimedOut_Fails()
    {
        var processed = Deploying().ProcessDeploymentStatus(null, new TimeoutException("too slow"));

        Assert.Equal(DeploymentStatus.Failed, processed.Status);
    }

    [Fact]
    public void ProcessDeploymentStatus_Cancelled_Unchanged()
    {
        var deploying = Deploying();

        var processed = deploying.ProcessDeploymentStatus(null, new OperationCanceledException());

        Assert.Same(deploying, processed);
    }

    [Fact]
    public void ProcessDeploymentStatus_NoExitCodeOrException_Throws()
    {
        Assert.Throws<ArgumentException>(() => Deploying().ProcessDeploymentStatus(null, null));
    }

    [Fact]
    public void ProcessDeploymentStatus_NotStarted_Throws()
    {
        Assert.Throws<InvalidOperationException>(() => NewDeployment().ProcessDeploymentStatus(0, null));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void ProcessDeploymentStatus_Finished_RejectsFurtherResults(long exitCode)
    {
        var finished = Deploying().ProcessDeploymentStatus(exitCode, null);

        Assert.Throws<InvalidOperationException>(() => finished.ProcessDeploymentStatus(0, null));
        Assert.Throws<InvalidOperationException>(() => finished.Start());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void StartDestroy_DeployedOrFailed_BecomesDestroying(long exitCode)
    {
        var finished = Deploying().ProcessDeploymentStatus(exitCode, null);

        Assert.True(finished.CanDestroy);
        Assert.Equal(DeploymentStatus.Destroying, finished.StartDestroy().Status);
    }

    [Fact]
    public void StartDestroy_NotFinished_Throws()
    {
        Assert.False(NewDeployment().CanDestroy);
        Assert.Throws<InvalidOperationException>(() => NewDeployment().StartDestroy());
        Assert.Throws<InvalidOperationException>(() => Deploying().StartDestroy());
        Assert.Throws<InvalidOperationException>(() => Destroying().StartDestroy());
        Assert.Throws<InvalidOperationException>(() => Destroying().ProcessDeploymentStatus(0, null).StartDestroy());
    }

    [Theory]
    [InlineData(0, DeploymentStatus.Destroyed)]
    [InlineData(1, DeploymentStatus.Failed)]
    public void ProcessDeploymentStatus_Destroying_DecidesStatus(long exitCode, DeploymentStatus expected)
    {
        Assert.Equal(expected, Destroying().ProcessDeploymentStatus(exitCode, null).Status);
    }

    [Fact]
    public void ProcessDeploymentStatus_DestroyingCancelled_Unchanged()
    {
        var destroying = Destroying();

        Assert.Same(destroying, destroying.ProcessDeploymentStatus(null, new OperationCanceledException()));
    }

    [Fact]
    public void StartDestroy_AfterFailedDestroy_CanRetry()
    {
        var failed = Destroying().ProcessDeploymentStatus(1, null);

        Assert.Equal(DeploymentStatus.Destroying, failed.StartDestroy().Status);
    }

    [Fact]
    public void IsActive_OnlyWhilePendingOrRunning()
    {
        Assert.True(NewDeployment().IsActive);
        Assert.True(Deploying().IsActive);
        Assert.True(Destroying().IsActive);
        Assert.False(Deploying().ProcessDeploymentStatus(0, null).IsActive);
        Assert.False(Deploying().ProcessDeploymentStatus(1, null).IsActive);
        Assert.False(Destroying().ProcessDeploymentStatus(0, null).IsActive);
    }

    [Fact]
    public void Interrupt_Active_BecomesFailed()
    {
        Assert.Equal(DeploymentStatus.Failed, NewDeployment().Interrupt("stopped").Status);
        Assert.Equal(DeploymentStatus.Failed, Deploying().Interrupt("stopped").Status);
        Assert.Equal(DeploymentStatus.Failed, Destroying().Interrupt("stopped").Status);
    }

    [Fact]
    public void Cancel_Active_BecomesCancelled()
    {
        var cancelled = Deploying().Cancel();

        Assert.Equal(DeploymentStatus.Cancelled, NewDeployment().Cancel().Status);
        Assert.Equal(DeploymentStatus.Cancelled, cancelled.Status);
        Assert.Equal(DeploymentStatus.Cancelled, Destroying().Cancel().Status);
        Assert.False(cancelled.IsActive);
        Assert.NotNull(cancelled.CompletedAt);
        Assert.Null(cancelled.ErrorSummary);
    }

    [Fact]
    public void Cancel_Finished_Throws()
    {
        Assert.Throws<InvalidOperationException>(() => Deploying().ProcessDeploymentStatus(0, null).Cancel());
        Assert.Throws<InvalidOperationException>(() => Deploying().Cancel().Cancel());
    }

    [Fact]
    public void StartDestroy_Cancelled_BecomesDestroying()
    {
        var cancelled = Deploying().Cancel();

        Assert.True(cancelled.CanDestroy);
        Assert.Equal(DeploymentStatus.Destroying, cancelled.StartDestroy().Status);
    }

    [Fact]
    public void Interrupt_Finished_Throws()
    {
        Assert.Throws<InvalidOperationException>(() => Deploying().ProcessDeploymentStatus(0, null).Interrupt("stopped"));
        Assert.Throws<InvalidOperationException>(() => Deploying().ProcessDeploymentStatus(1, null).Interrupt("stopped"));
    }

    [Theory]
    [InlineData("abc123")]
    [InlineData("main")]
    [InlineData("")]
    public void Create_InvalidCommitId_Throws(string commitId)
    {
        Assert.Throws<ArgumentException>(() =>
            Deployment.Create(new ApplicationId(), new EnvironmentId(), new CommitId(commitId), "test@example.com"));
    }

    [Fact]
    public void Create_SetsRequestedByAndNoRunTimestamps()
    {
        var deployment = NewDeployment();

        Assert.Equal("test@example.com", deployment.RequestedBy);
        Assert.Null(deployment.StartedAt);
        Assert.Null(deployment.CompletedAt);
        Assert.Null(deployment.ErrorSummary);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void Create_BlankRequestedBy_Throws(string requestedBy)
    {
        Assert.Throws<ArgumentException>(() =>
            Deployment.Create(new ApplicationId(), new EnvironmentId(), new CommitId(new string('a', 40)),
                requestedBy));
    }

    [Fact]
    public void Create_RequestedByTooLong_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            Deployment.Create(new ApplicationId(), new EnvironmentId(), new CommitId(new string('a', 40)),
                new string('a', Deployment.RequestedByMaxLength + 1)));
    }

    [Fact]
    public void Start_SetsStartedAt()
    {
        var started = Deploying();

        Assert.NotNull(started.StartedAt);
        Assert.Equal(started.UpdatedAt, started.StartedAt);
        Assert.Null(started.CompletedAt);
    }

    [Fact]
    public void ProcessDeploymentStatus_Succeeded_SetsCompletedAtWithoutError()
    {
        var deploying = Deploying();

        var deployed = deploying.ProcessDeploymentStatus(0, null);

        Assert.Equal(deploying.StartedAt, deployed.StartedAt);
        Assert.NotNull(deployed.CompletedAt);
        Assert.True(deployed.CompletedAt >= deployed.StartedAt);
        Assert.Null(deployed.ErrorSummary);
    }

    [Fact]
    public void ProcessDeploymentStatus_NonZeroExitCode_SetsErrorSummary()
    {
        var failed = Deploying().ProcessDeploymentStatus(137, null);

        Assert.NotNull(failed.CompletedAt);
        Assert.Equal("The runner exited with code 137.", failed.ErrorSummary);
    }

    [Fact]
    public void ProcessDeploymentStatus_Exception_SetsErrorSummaryFromMessage()
    {
        var failed = Deploying().ProcessDeploymentStatus(null, new InvalidOperationException("image missing"));

        Assert.NotNull(failed.CompletedAt);
        Assert.Equal("image missing", failed.ErrorSummary);
    }

    [Fact]
    public void ProcessDeploymentStatus_LongExceptionMessage_TruncatesErrorSummary()
    {
        var message = new string('x', Deployment.ErrorSummaryMaxLength + 10);

        var failed = Deploying().ProcessDeploymentStatus(null, new InvalidOperationException(message));

        Assert.Equal(Deployment.ErrorSummaryMaxLength, failed.ErrorSummary?.Length);
    }

    [Fact]
    public void ProcessDeploymentStatus_Cancelled_KeepsCompletedAtUnset()
    {
        var processed = Deploying().ProcessDeploymentStatus(null, new OperationCanceledException());

        Assert.Null(processed.CompletedAt);
        Assert.Null(processed.ErrorSummary);
    }

    [Fact]
    public void Interrupt_SetsCompletedAtAndErrorSummary()
    {
        var interrupted = NewDeployment().Interrupt("could not queue");

        Assert.Null(interrupted.StartedAt);
        Assert.NotNull(interrupted.CompletedAt);
        Assert.Equal("could not queue", interrupted.ErrorSummary);
    }

    [Fact]
    public void StartDestroy_AfterFailure_ResetsRunFields()
    {
        var failed = Deploying().ProcessDeploymentStatus(1, null);

        var destroying = failed.StartDestroy();

        Assert.True(destroying.StartedAt >= failed.CompletedAt);
        Assert.Null(destroying.CompletedAt);
        Assert.Null(destroying.ErrorSummary);
        Assert.Equal(failed.RequestedBy, destroying.RequestedBy);
    }

    [Fact]
    public void ProcessDeploymentStatus_Destroyed_SetsCompletedAt()
    {
        var destroyed = Destroying().ProcessDeploymentStatus(0, null);

        Assert.NotNull(destroyed.CompletedAt);
        Assert.Null(destroyed.ErrorSummary);
    }

    private static Deployment NewDeployment() =>
        Deployment.Create(new ApplicationId(), new EnvironmentId(), new CommitId(new string('a', 40)), "test@example.com");

    private static Deployment Deploying() => NewDeployment().Start();

    private static Deployment Destroying() => Deploying().ProcessDeploymentStatus(0, null).StartDestroy();
}
