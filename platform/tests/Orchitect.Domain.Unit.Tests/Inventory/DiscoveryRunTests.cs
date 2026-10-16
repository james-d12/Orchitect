using Orchitect.Domain.Inventory.Discovery;

namespace Orchitect.Domain.Unit.Tests.Inventory;

public sealed class DiscoveryRunTests
{
    [Fact]
    public void Start_CreatesRunningRunForConfiguration()
    {
        var configurationId = new DiscoveryConfigurationId();

        var run = DiscoveryRun.Start(configurationId);

        Assert.Equal(configurationId, run.DiscoveryConfigurationId);
        Assert.Equal(DiscoveryRunStatus.Running, run.Status);
        Assert.Null(run.CompletedAt);
        Assert.Null(run.ErrorMessage);
        Assert.Equal(0, run.Counts.Total);
    }

    [Fact]
    public void Succeed_Running_RecordsCounts()
    {
        var counts = new DiscoveryCounts { Teams = 1, Repositories = 2, CloudSecrets = 4 };

        var succeeded = Running().Succeed(counts);

        Assert.Equal(DiscoveryRunStatus.Succeeded, succeeded.Status);
        Assert.NotNull(succeeded.CompletedAt);
        Assert.Equal(counts, succeeded.Counts);
        Assert.Equal(7, succeeded.Counts.Total);
    }

    [Fact]
    public void Fail_Running_RecordsError()
    {
        var failed = Running().Fail("Unauthorised");

        Assert.Equal(DiscoveryRunStatus.Failed, failed.Status);
        Assert.NotNull(failed.CompletedAt);
        Assert.Equal("Unauthorised", failed.ErrorMessage);
    }

    [Fact]
    public void Fail_LongError_IsTruncated()
    {
        var failed = Running().Fail(new string('x', DiscoveryRun.ErrorMessageMaxLength + 10));

        Assert.Equal(DiscoveryRun.ErrorMessageMaxLength, failed.ErrorMessage?.Length);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void Fail_WithoutError_Throws(string errorMessage)
    {
        Assert.ThrowsAny<ArgumentException>(() => Running().Fail(errorMessage));
    }

    [Fact]
    public void Finish_AlreadyFinished_Throws()
    {
        var succeeded = Running().Succeed(new DiscoveryCounts());
        var failed = Running().Fail("Unauthorised");

        Assert.Throws<InvalidOperationException>(() => succeeded.Fail("Unauthorised"));
        Assert.Throws<InvalidOperationException>(() => failed.Succeed(new DiscoveryCounts()));
    }

    private static DiscoveryRun Running() => DiscoveryRun.Start(new DiscoveryConfigurationId());
}
