using Microsoft.Extensions.Logging.Abstractions;
using Orchitect.Engine.Execution.Provisioner.Helm;
using Orchitect.Engine.Execution.Provisioner.Helm.Models;

namespace Orchitect.Engine.Execution.Unit.Tests.Helm;

public sealed class HelmParserTests : IDisposable
{
    private readonly string _directory = Directory.CreateTempSubdirectory("orchitect-helm-parser-tests").FullName;
    private readonly HelmParser _parser = new(NullLogger<HelmParser>.Instance);

    public void Dispose() => Directory.Delete(_directory, recursive: true);

    [Fact]
    public async Task ParseHelmConfigAsync_NestedValues_FlattensToDottedKeys()
    {
        await File.WriteAllTextAsync(Path.Combine(_directory, "values.yaml"),
            """
            replicaCount: 2
            image:
              repository: nginx
              tag:
            ports:
              - 80
              - name: https
                port: 443
            """);

        var inputs = await _parser.ParseHelmConfigAsync(_directory);

        Assert.Equal(
        [
            new HelmInput("replicaCount", "2"),
            new HelmInput("image.repository", "nginx"),
            new HelmInput("image.tag", null),
            new HelmInput("ports[0]", "80"),
            new HelmInput("ports[1].name", "https"),
            new HelmInput("ports[1].port", "443")
        ], inputs);
    }

    [Fact]
    public async Task ParseHelmConfigAsync_ValuesInSubdirectory_IsFound()
    {
        var chart = Directory.CreateDirectory(Path.Combine(_directory, "charts", "orders")).FullName;
        await File.WriteAllTextAsync(Path.Combine(chart, "values.yaml"), "name: orders");

        var inputs = await _parser.ParseHelmConfigAsync(_directory);

        Assert.Equal([new HelmInput("name", "orders")], inputs);
    }

    [Fact]
    public async Task ParseHelmConfigAsync_NoValuesFile_ReturnsEmpty()
    {
        var inputs = await _parser.ParseHelmConfigAsync(_directory);

        Assert.Empty(inputs);
    }
}
