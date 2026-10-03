using Orchitect.Domain.Engine.Deployment;
using Orchitect.Domain.Engine.ResourceInstance;

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

    [Fact]
    public void Succeed_Running_RecordsExitCodeAndRunner()
    {
        var succeeded = Running().Succeed(0, "container-1");

        Assert.Equal(DeploymentRunStatus.Succeeded, succeeded.Status);
        Assert.Equal(0, succeeded.ExitCode);
        Assert.Equal("container-1", succeeded.RunnerId);
        Assert.Null(succeeded.ErrorSummary);
        Assert.NotNull(succeeded.FinishedAt);
        Assert.False(succeeded.IsActive);
    }

    [Fact]
    public void Succeed_FromReport_LeavesExitCodeUnset()
    {
        var succeeded = Running().Succeed();

        Assert.Equal(DeploymentRunStatus.Succeeded, succeeded.Status);
        Assert.Null(succeeded.ExitCode);
    }

    [Fact]
    public void Succeed_NotRunning_Throws()
    {
        Assert.Throws<InvalidOperationException>(() => Queued().Succeed(0));
        Assert.Throws<InvalidOperationException>(() => Running().Fail("failed").Succeed(0));
    }

    [Fact]
    public void Fail_Running_RecordsErrorSummary()
    {
        var failed = Running().Fail("The runner exited with code 2.", 2, "container-1");

        Assert.Equal(DeploymentRunStatus.Failed, failed.Status);
        Assert.Equal("The runner exited with code 2.", failed.ErrorSummary);
        Assert.Equal(2, failed.ExitCode);
        Assert.Equal("container-1", failed.RunnerId);
        Assert.NotNull(failed.FinishedAt);
    }

    [Fact]
    public void Fail_Queued_Fails()
    {
        var failed = Queued().Fail("could not queue");

        Assert.Equal(DeploymentRunStatus.Failed, failed.Status);
        Assert.Equal("could not queue", failed.ErrorSummary);
        Assert.NotNull(failed.FinishedAt);
    }

    [Fact]
    public void Fail_LongErrorSummary_IsTruncated()
    {
        var failed = Running().Fail(new string('x', 5000));

        Assert.Equal(DeploymentRun.ErrorSummaryMaxLength, failed.ErrorSummary!.Length);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void Fail_BlankErrorSummary_Throws(string errorSummary)
    {
        Assert.Throws<ArgumentException>(() => Running().Fail(errorSummary));
    }

    [Fact]
    public void Fail_Finished_Throws()
    {
        Assert.Throws<InvalidOperationException>(() => Running().Succeed(0).Fail("late"));
    }

    [Fact]
    public void RecordExit_AfterReport_KeepsOutcomeAndRecordsExitCode()
    {
        var reported = Running().Fail("terraform apply failed");

        var recorded = reported.RecordExit(0, "container-1");

        Assert.Equal(DeploymentRunStatus.Failed, recorded.Status);
        Assert.Equal("terraform apply failed", recorded.ErrorSummary);
        Assert.Equal(0, recorded.ExitCode);
        Assert.Equal("container-1", recorded.RunnerId);
    }

    [Fact]
    public void RecordExit_ExitCodeAlreadyRecorded_KeepsIt()
    {
        var finished = Running().Fail("The runner exited with code 1.", 1, "container-1");

        Assert.Equal(finished, finished.RecordExit(0, "container-2"));
    }

    [Fact]
    public void RecordExit_Active_Throws()
    {
        Assert.Throws<InvalidOperationException>(() => Running().RecordExit(0));
    }

    [Fact]
    public void RecordPlan_Running_RecordsProjectAndDistinctInstances()
    {
        var instanceId = new ResourceInstanceId();

        var planned = Running().RecordPlan("orders", [instanceId, instanceId]);

        Assert.Equal("orders", planned.ProjectName);
        Assert.Equal([instanceId], planned.InstanceIds);
    }

    [Fact]
    public void RecordPlan_NotRunning_Throws()
    {
        Assert.Throws<InvalidOperationException>(() => Queued().RecordPlan("orders", []));
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
            Running().Succeed(0).IssueToken("hash", DateTime.UtcNow.AddHours(1)));
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
    public void Succeed_RevokesToken()
    {
        var succeeded = Running().IssueToken("hash", DateTime.UtcNow.AddHours(1)).Succeed(0);

        Assert.Null(succeeded.TokenHash);
        Assert.Null(succeeded.TokenExpiresAt);
    }

    [Fact]
    public void Fail_RevokesToken()
    {
        var failed = Running().IssueToken("hash", DateTime.UtcNow.AddHours(1)).Fail("lost");

        Assert.Null(failed.TokenHash);
        Assert.Null(failed.TokenExpiresAt);
    }

    private static DeploymentRun Queued() => DeploymentRun.Queue(new DeploymentId(), DeploymentRunOperation.Provision);

    private static DeploymentRun Running() => Queued().Start();
}
