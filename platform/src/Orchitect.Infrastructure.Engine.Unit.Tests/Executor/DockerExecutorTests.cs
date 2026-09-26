using Docker.DotNet;
using Docker.DotNet.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Npgsql;
using Orchitect.Infrastructure.Engine.Executor;

namespace Orchitect.Infrastructure.Engine.Unit.Tests.Executor;

public sealed class DockerExecutorTests
{
    private const string ContainerId = "0123456789abcdef";

    private readonly IContainerOperations _containers = Substitute.For<IContainerOperations>();
    private readonly CancellationTokenSource _cancellation = new();
    private readonly DockerExecutor _executor;

    public DockerExecutorTests()
    {
        var docker = Substitute.For<IDockerClient>();
        var images = Substitute.For<IImageOperations>();
        docker.Containers.Returns(_containers);
        docker.Images.Returns(images);

        images.InspectImageAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ImageInspectResponse { ID = "sha256:runner" });
        _containers.CreateContainerAsync(Arg.Any<CreateContainerParameters>(), Arg.Any<CancellationToken>())
            .Returns(new CreateContainerResponse { ID = ContainerId });
        _containers.StartContainerAsync(Arg.Any<string>(), Arg.Any<ContainerStartParameters>(),
                Arg.Any<CancellationToken>())
            .Returns(true);
        _containers.GetContainerLogsAsync(Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<ContainerLogsParameters>(),
                Arg.Any<CancellationToken>())
            .Returns(_ => new MultiplexedStream(new MemoryStream(), true));
        SetRunning(false);

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:orchitect"] = "Host=localhost;Port=41031;Database=orchitect"
            })
            .Build();

        _executor = new DockerExecutor(NullLogger<DockerExecutor>.Instance, docker, configuration);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public async Task ExecuteAsync_ContainerExits_ReturnsExitCodeAndRemovesContainer(long exitCode)
    {
        SetWait(_ => Task.FromResult(new ContainerWaitResponse { StatusCode = exitCode }));

        var result = await ExecuteAsync();

        Assert.Equal(exitCode, result.ExitCode);
        Assert.Null(result.Exception);
        await AssertRemovedAsync();
    }

    [Fact]
    public async Task ExecuteAsync_CancelledDuringStartWhileRunning_DetachesContainer()
    {
        SetRunning(true);
        _containers.StartContainerAsync(Arg.Any<string>(), Arg.Any<ContainerStartParameters>(),
                Arg.Any<CancellationToken>())
            .Returns(_ => CancelThen<bool>());

        var result = await ExecuteAsync();

        Assert.IsAssignableFrom<OperationCanceledException>(result.Exception);
        await AssertNotRemovedAsync();
    }

    [Fact]
    public async Task ExecuteAsync_CancelledDuringStartBeforeRunning_RemovesContainer()
    {
        _containers.StartContainerAsync(Arg.Any<string>(), Arg.Any<ContainerStartParameters>(),
                Arg.Any<CancellationToken>())
            .Returns(_ => CancelThen<bool>());

        var result = await ExecuteAsync();

        Assert.IsAssignableFrom<OperationCanceledException>(result.Exception);
        await AssertRemovedAsync();
    }

    [Fact]
    public async Task ExecuteAsync_CancelledWhileWaitingForRunningContainer_DetachesContainer()
    {
        SetRunning(true);
        SetWait(_ => CancelThen<ContainerWaitResponse>());

        var result = await ExecuteAsync();

        Assert.IsAssignableFrom<OperationCanceledException>(result.Exception);
        await AssertNotRemovedAsync();
    }

    [Fact]
    public async Task ExecuteAsync_CancelledAfterExitWhileDrainingOutput_KeepsExitCodeAndRemovesContainer()
    {
        _containers.GetContainerLogsAsync(Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<ContainerLogsParameters>(),
                Arg.Any<CancellationToken>())
            .Returns(_ => new MultiplexedStream(new BlockingStream(), true));
        SetWait(_ =>
        {
            _cancellation.Cancel();
            return Task.FromResult(new ContainerWaitResponse { StatusCode = 0 });
        });

        var result = await ExecuteAsync();

        Assert.Equal(0, result.ExitCode);
        Assert.Null(result.Exception);
        await AssertRemovedAsync();
    }

    [Fact]
    public async Task ExecuteAsync_TimesOut_SendsSigtermAndRemovesContainer()
    {
        var waits = 0;
        SetWait(call => Interlocked.Increment(ref waits) == 1
            ? WaitUntilCancelledAsync(call.ArgAt<CancellationToken>(1))
            : Task.FromResult(new ContainerWaitResponse { StatusCode = 143 }));

        var result = await ExecuteAsync(TimeSpan.FromMilliseconds(50), TimeSpan.FromSeconds(5));

        Assert.IsType<TimeoutException>(result.Exception);
        await _containers.Received(1).KillContainerAsync(ContainerId,
            Arg.Is<ContainerKillParameters>(p => p.Signal == "SIGTERM"), Arg.Any<CancellationToken>());
        await AssertRemovedAsync();
    }

    [Fact]
    public async Task ExecuteAsync_TimesOutAndIgnoresSigterm_RemovesContainerAfterGracePeriod()
    {
        SetRunning(true);
        SetWait(call => WaitUntilCancelledAsync(call.ArgAt<CancellationToken>(1)));

        var result = await ExecuteAsync(TimeSpan.FromMilliseconds(50), TimeSpan.FromMilliseconds(50));

        Assert.IsType<TimeoutException>(result.Exception);
        await AssertRemovedAsync();
    }

    [Fact]
    public async Task ExecuteAsync_CancelledAndInspectFails_LeavesContainer()
    {
        _containers.InspectContainerAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("daemon unavailable"));
        SetWait(_ => CancelThen<ContainerWaitResponse>());

        var result = await ExecuteAsync();

        Assert.IsAssignableFrom<OperationCanceledException>(result.Exception);
        await AssertNotRemovedAsync();
    }

    private Task<ExecutorResult> ExecuteAsync(TimeSpan? timeout = null, TimeSpan? stopGracePeriod = null) =>
        _executor.ExecuteAsync(new ExecutorContext
        {
            Image = "orchitect-runner:test",
            RunId = "run-1",
            Arguments = [],
            Configuration = new Dictionary<string, string>(),
            Timeout = timeout,
            StopGracePeriod = stopGracePeriod
        }, _cancellation.Token);

    private void SetRunning(bool running) =>
        _containers.InspectContainerAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ContainerInspectResponse { State = new ContainerState { Running = running } });

    private void SetWait(Func<NSubstitute.Core.CallInfo, Task<ContainerWaitResponse>> wait) =>
        _containers.WaitContainerAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(wait);

    private Task<T> CancelThen<T>()
    {
        _cancellation.Cancel();
        return Task.FromCanceled<T>(_cancellation.Token);
    }

    private static async Task<ContainerWaitResponse> WaitUntilCancelledAsync(CancellationToken cancellationToken)
    {
        await Task.Delay(Timeout.Infinite, cancellationToken);
        throw new InvalidOperationException("Unreachable.");
    }

    private Task AssertRemovedAsync() =>
        _containers.Received(1).RemoveContainerAsync(ContainerId,
            Arg.Is<ContainerRemoveParameters>(p => p.Force == true), Arg.Any<CancellationToken>());

    private Task AssertNotRemovedAsync() =>
        _containers.DidNotReceive().RemoveContainerAsync(Arg.Any<string>(), Arg.Any<ContainerRemoveParameters>(),
            Arg.Any<CancellationToken>());

    private sealed class BlockingStream : Stream
    {
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count,
            CancellationToken cancellationToken)
        {
            await Task.Delay(Timeout.Infinite, cancellationToken);
            return 0;
        }

        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }

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
