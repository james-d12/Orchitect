using Orchitect.Domain.Engine.Deployment;

namespace Orchitect.Domain.Unit.Tests.Engine;

public sealed class DeploymentRunTests
{
    [Fact]
    public void Queue_CreatesQueuedRunForDeployment()
    {
        var deploymentId = new DeploymentId();

        var run = DeploymentRun.Queue(deploymentId, DeploymentRunOperation.Destroy);

        Assert.Equal(deploymentId, run.DeploymentId);
        Assert.Equal(DeploymentRunOperation.Destroy, run.Operation);
        Assert.Equal(DeploymentRunStatus.Queued, run.Status);
        Assert.True(run.IsActive);
        Assert.Null(run.StartedAt);
        Assert.Null(run.FinishedAt);
    }

    [Fact]
    public void Queue_EachRunHasItsOwnId()
    {
        var deploymentId = new DeploymentId();

        var first = DeploymentRun.Queue(deploymentId, DeploymentRunOperation.Provision);
        var second = DeploymentRun.Queue(deploymentId, DeploymentRunOperation.Provision);

        Assert.NotEqual(first.Id, second.Id);
        Assert.NotEqual(deploymentId.Value, first.Id.Value);
    }

    [Fact]
    public void Start_Queued_BecomesRunning()
    {
        var started = Queued().Start();

        Assert.Equal(DeploymentRunStatus.Running, started.Status);
        Assert.NotNull(started.StartedAt);
    }

    [Fact]
    public void Start_AlreadyStarted_Throws()
    {
        Assert.Throws<InvalidOperationException>(() => Running().Start());
    }

    [Theory]
    [InlineData(0, DeploymentRunStatus.Succeeded)]
    [InlineData(1, DeploymentRunStatus.Failed)]
    [InlineData(137, DeploymentRunStatus.Failed)]
    public void Complete_ExitCode_DecidesStatusAndIsRecorded(long exitCode, DeploymentRunStatus expected)
    {
        var completed = Running().Complete(exitCode, null, "container-1");

        Assert.Equal(expected, completed.Status);
        Assert.Equal(exitCode, completed.ExitCode);
        Assert.Equal("container-1", completed.RunnerId);
        Assert.NotNull(completed.FinishedAt);
        Assert.False(completed.IsActive);
    }

    [Fact]
    public void Complete_NonZeroExitCode_SetsErrorSummary()
    {
        var completed = Running().Complete(2, null);

        Assert.Equal("The runner exited with code 2.", completed.ErrorSummary);
    }

    [Fact]
    public void Complete_Exception_FailsWithMessage()
    {
        var completed = Running().Complete(null, new TimeoutException("too slow"));

        Assert.Equal(DeploymentRunStatus.Failed, completed.Status);
        Assert.Equal("too slow", completed.ErrorSummary);
        Assert.Null(completed.ExitCode);
    }

    [Fact]
    public void Complete_Cancelled_StaysRunning()
    {
        var running = Running();

        var completed = running.Complete(null, new OperationCanceledException());

        Assert.Equal(running, completed);
    }

    [Fact]
    public void Complete_LongErrorSummary_IsTruncated()
    {
        var completed = Running().Complete(null, new InvalidOperationException(new string('x', 5000)));

        Assert.Equal(DeploymentRun.ErrorSummaryMaxLength, completed.ErrorSummary!.Length);
    }

    [Fact]
    public void Complete_NoResult_Throws()
    {
        Assert.Throws<ArgumentException>(() => Running().Complete(null, null));
    }

    [Fact]
    public void Complete_NotRunning_Throws()
    {
        Assert.Throws<InvalidOperationException>(() => Queued().Complete(0, null));
    }

    [Fact]
    public void Interrupt_Queued_Fails()
    {
        var interrupted = Queued().Interrupt("could not queue");

        Assert.Equal(DeploymentRunStatus.Failed, interrupted.Status);
        Assert.Equal("could not queue", interrupted.ErrorSummary);
        Assert.NotNull(interrupted.FinishedAt);
    }

    [Fact]
    public void Interrupt_Finished_Throws()
    {
        Assert.Throws<InvalidOperationException>(() => Running().Complete(0, null).Interrupt("late"));
    }

    [Fact]
    public void IssueToken_Running_StoresHashAndExpiry()
    {
        var expiresAt = DateTime.UtcNow.AddHours(1);

        var run = Running().IssueToken("hash", expiresAt);

        Assert.Equal("hash", run.TokenHash);
        Assert.Equal(expiresAt, run.TokenExpiresAt);
    }

    [Fact]
    public void IssueToken_Finished_Throws()
    {
        Assert.Throws<InvalidOperationException>(() =>
            Running().Complete(0, null).IssueToken("hash", DateTime.UtcNow.AddHours(1)));
    }

    [Fact]
    public void IssueToken_EmptyHash_Throws()
    {
        Assert.Throws<ArgumentException>(() => Running().IssueToken(" ", DateTime.UtcNow.AddHours(1)));
    }

    [Fact]
    public void HasValidToken_ActiveAndUnexpired_IsTrue()
    {
        var now = DateTime.UtcNow;

        Assert.True(Queued().IssueToken("hash", now.AddMinutes(1)).HasValidToken(now));
        Assert.True(Running().IssueToken("hash", now.AddMinutes(1)).HasValidToken(now));
    }

    [Fact]
    public void HasValidToken_Expired_IsFalse()
    {
        var now = DateTime.UtcNow;

        Assert.False(Running().IssueToken("hash", now).HasValidToken(now));
    }

    [Fact]
    public void HasValidToken_NoToken_IsFalse()
    {
        Assert.False(Running().HasValidToken(DateTime.UtcNow));
    }

    [Theory]
    [InlineData(DeploymentRunStatus.Succeeded)]
    [InlineData(DeploymentRunStatus.Failed)]
    [InlineData(DeploymentRunStatus.Cancelled)]
    public void HasValidToken_RunNotActive_IsFalse(DeploymentRunStatus status)
    {
        var now = DateTime.UtcNow;
        var run = Running().IssueToken("hash", now.AddHours(1)) with { Status = status };

        Assert.False(run.HasValidToken(now));
    }

    [Fact]
    public void Complete_RevokesToken()
    {
        var completed = Running().IssueToken("hash", DateTime.UtcNow.AddHours(1)).Complete(0, null);

        Assert.Null(completed.TokenHash);
        Assert.Null(completed.TokenExpiresAt);
    }

    [Fact]
    public void Interrupt_RevokesToken()
    {
        var interrupted = Running().IssueToken("hash", DateTime.UtcNow.AddHours(1)).Interrupt("lost");

        Assert.Null(interrupted.TokenHash);
        Assert.Null(interrupted.TokenExpiresAt);
    }

    [Fact]
    public void Complete_Cancelled_KeepsToken()
    {
        var running = Running().IssueToken("hash", DateTime.UtcNow.AddHours(1));

        Assert.Equal("hash", running.Complete(null, new OperationCanceledException()).TokenHash);
    }

    private static DeploymentRun Queued() => DeploymentRun.Queue(new DeploymentId(), DeploymentRunOperation.Provision);

    private static DeploymentRun Running() => Queued().Start();
}
