using Npgsql;
using Orchitect.Infrastructure.Engine.Executor;

namespace Orchitect.Infrastructure.Engine.Unit.Tests.Executor;

public sealed class DockerExecutorTests
{
    [Theory]
    [InlineData("Server=localhost;Port=41031;Database=orchitect")]
    [InlineData("Host=127.0.0.1;Port=41031;Database=orchitect")]
    public void RewriteConnectionString_LoopbackHost_PointsAtDockerHost(string connectionString)
    {
        var rewritten = new NpgsqlConnectionStringBuilder(
            DockerExecutor.RewriteConnectionString(connectionString, null, null));

        Assert.Equal("host.docker.internal", rewritten.Host);
        Assert.Equal(41031, rewritten.Port);
    }

    [Fact]
    public void RewriteConnectionString_DatabaseHostAndPortSet_OverridesAliasedHost()
    {
        var rewritten = DockerExecutor.RewriteConnectionString(
            "Server=localhost;Port=41031;Database=orchitect", "postgres", 5432);

        var builder = new NpgsqlConnectionStringBuilder(rewritten);
        Assert.Equal("postgres", builder.Host);
        Assert.Equal(5432, builder.Port);
        Assert.DoesNotContain("localhost", rewritten);
    }
}
