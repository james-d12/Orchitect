using Orchitect.Domain.Engine.Deployment;
using Orchitect.Domain.Engine.Environment;
using ApplicationId = Orchitect.Domain.Engine.Application.ApplicationId;

namespace Orchitect.Infrastructure.Engine.Unit.Tests.Domain;

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

    private static Deployment NewDeployment() =>
        Deployment.Create(new ApplicationId(), new EnvironmentId(), new CommitId("abc123"));

    private static Deployment Deploying() => NewDeployment().Start();
}
