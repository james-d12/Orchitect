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

    [Fact]
    public void Succeed_Deploying_BecomesDeployed()
    {
        Assert.Equal(DeploymentStatus.Deployed, Deploying().Succeed().Status);
    }

    [Fact]
    public void Fail_Deploying_BecomesFailed()
    {
        Assert.Equal(DeploymentStatus.Failed, Deploying().Fail("terraform apply failed").Status);
    }

    [Fact]
    public void Succeed_NotStarted_Throws()
    {
        Assert.Throws<InvalidOperationException>(() => NewDeployment().Succeed());
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void Fail_BlankErrorSummary_Throws(string errorSummary)
    {
        Assert.Throws<ArgumentException>(() => Deploying().Fail(errorSummary));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Finished_RejectsFurtherResults(bool succeeded)
    {
        var finished = Finish(Deploying(), succeeded);

        Assert.Throws<InvalidOperationException>(() => finished.Succeed());
        Assert.Throws<InvalidOperationException>(() => finished.Fail("late"));
        Assert.Throws<InvalidOperationException>(() => finished.Start());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void StartDestroy_DeployedOrFailed_BecomesDestroying(long exitCode)
    {
        var finished = Finish(Deploying(), exitCode == 0);

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
        Assert.Throws<InvalidOperationException>(() => Destroying().Succeed().StartDestroy());
    }

    [Theory]
    [InlineData(true, DeploymentStatus.Destroyed)]
    [InlineData(false, DeploymentStatus.Failed)]
    public void Finish_Destroying_DecidesStatus(bool succeeded, DeploymentStatus expected)
    {
        Assert.Equal(expected, Finish(Destroying(), succeeded).Status);
    }

    [Fact]
    public void StartDestroy_AfterFailedDestroy_CanRetry()
    {
        var failed = Destroying().Fail("terraform destroy failed");

        Assert.Equal(DeploymentStatus.Destroying, failed.StartDestroy().Status);
    }

    [Fact]
    public void IsActive_OnlyWhilePendingOrRunning()
    {
        Assert.True(NewDeployment().IsActive);
        Assert.True(Deploying().IsActive);
        Assert.True(Destroying().IsActive);
        Assert.False(Deploying().Succeed().IsActive);
        Assert.False(Deploying().Fail("failed").IsActive);
        Assert.False(Destroying().Succeed().IsActive);
    }

    [Fact]
    public void Fail_Active_BecomesFailed()
    {
        Assert.Equal(DeploymentStatus.Failed, NewDeployment().Fail("stopped").Status);
        Assert.Equal(DeploymentStatus.Failed, Deploying().Fail("stopped").Status);
        Assert.Equal(DeploymentStatus.Failed, Destroying().Fail("stopped").Status);
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
    public void Succeed_SetsCompletedAtWithoutError()
    {
        var deploying = Deploying();

        var deployed = deploying.Succeed();

        Assert.Equal(deploying.StartedAt, deployed.StartedAt);
        Assert.NotNull(deployed.CompletedAt);
        Assert.True(deployed.CompletedAt >= deployed.StartedAt);
        Assert.Null(deployed.ErrorSummary);
    }

    [Fact]
    public void Fail_SetsCompletedAtAndErrorSummary()
    {
        var failed = Deploying().Fail("The runner exited with code 137.");

        Assert.NotNull(failed.CompletedAt);
        Assert.Equal("The runner exited with code 137.", failed.ErrorSummary);
    }

    [Fact]
    public void Fail_LongErrorSummary_IsTruncated()
    {
        var failed = Deploying().Fail(new string('x', Deployment.ErrorSummaryMaxLength + 10));

        Assert.Equal(Deployment.ErrorSummaryMaxLength, failed.ErrorSummary?.Length);
    }

    [Fact]
    public void Fail_Pending_SetsCompletedAtWithoutStartedAt()
    {
        var failed = NewDeployment().Fail("could not queue");

        Assert.Null(failed.StartedAt);
        Assert.NotNull(failed.CompletedAt);
        Assert.Equal("could not queue", failed.ErrorSummary);
    }

    [Fact]
    public void StartDestroy_AfterFailure_ResetsRunFields()
    {
        var failed = Deploying().Fail("terraform apply failed");

        var destroying = failed.StartDestroy();

        Assert.True(destroying.StartedAt >= failed.CompletedAt);
        Assert.Null(destroying.CompletedAt);
        Assert.Null(destroying.ErrorSummary);
        Assert.Equal(failed.RequestedBy, destroying.RequestedBy);
    }

    [Fact]
    public void Succeed_Destroying_SetsCompletedAt()
    {
        var destroyed = Destroying().Succeed();

        Assert.NotNull(destroyed.CompletedAt);
        Assert.Null(destroyed.ErrorSummary);
    }

    private static Deployment NewDeployment() =>
        Deployment.Create(new ApplicationId(), new EnvironmentId(), new CommitId(new string('a', 40)), "test@example.com");

    private static Deployment Deploying() => NewDeployment().Start();

    private static Deployment Destroying() => Deploying().Succeed().StartDestroy();

    private static Deployment Finish(Deployment deployment, bool succeeded) =>
        succeeded ? deployment.Succeed() : deployment.Fail("The runner exited with code 1.");
}
