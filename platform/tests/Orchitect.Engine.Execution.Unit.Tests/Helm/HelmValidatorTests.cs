using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Orchitect.Engine.Contracts.Runner.Api;
using Orchitect.Engine.Execution.Provisioner.Helm;
using Orchitect.Engine.Execution.Provisioner.Helm.Models;
using Orchitect.Engine.Execution.Unit.Tests.Terraform;

namespace Orchitect.Engine.Execution.Unit.Tests.Helm;

public sealed class HelmValidatorTests : IDisposable
{
    private readonly TerraformModuleDownloaderTests.FakeGitCommandLine _git = new();
    private readonly IHelmParser _parser = Substitute.For<IHelmParser>();
    private readonly string _templateName = $"orders-{Guid.NewGuid():N}";

    public HelmValidatorTests()
    {
        _parser.ParseHelmConfigAsync(Arg.Any<string>())
            .Returns([new HelmInput("replicaCount", 1), new HelmInput("image.tag", "latest")]);
    }

    public void Dispose()
    {
        var cloneDirectory = Path.Combine(Path.GetTempPath(), "orchitect", "helm", _templateName);

        if (Directory.Exists(cloneDirectory))
        {
            Directory.Delete(cloneDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task ValidateAsync_NonHelmProvider_ReturnsWrongProvider()
    {
        var result = await CreateValidator().ValidateAsync(
            Input(RunInputProvider.Terraform));

        Assert.Equal(HelmValidationResultState.WrongProvider, result.State);
        Assert.Null(result.Config);
        Assert.Equal(0, _git.CloneCount);
    }

    [Fact]
    public async Task ValidateAsync_CloneFails_ReturnsModuleNotFound()
    {
        _git.Succeeds = false;

        var result = await CreateValidator().ValidateAsync(Input());

        Assert.Equal(HelmValidationResultState.ModuleNotFound, result.State);
        await _parser.DidNotReceiveWithAnyArgs().ParseHelmConfigAsync(default!);
    }

    [Fact]
    public async Task ValidateAsync_UnknownInput_ReturnsInputNotPresent()
    {
        var result = await CreateValidator().ValidateAsync(Input(
            parameters: new Dictionary<string, string> { ["replicaCount"] = "3", ["missing.key"] = "x" }));

        Assert.Equal(HelmValidationResultState.InputNotPresent, result.State);
        Assert.Contains("missing.key", result.Message);
        Assert.DoesNotContain("replicaCount", result.Message);
    }

    [Fact]
    public async Task ValidateAsync_KnownInputsCaseInsensitive_ReturnsValidWithConfig()
    {
        var result = await CreateValidator().ValidateAsync(Input(
            parameters: new Dictionary<string, string> { ["REPLICACOUNT"] = "3", ["image.tag"] = "v2" }));

        Assert.Equal(HelmValidationResultState.Valid, result.State);
        Assert.NotNull(result.Config);
        Assert.Equal(2, result.Config.Count);
    }

    [Fact]
    public async Task ValidateAsync_FolderPathSet_ParsesSubfolder()
    {
        await CreateValidator().ValidateAsync(Input(folderPath: "charts/orders"));

        await _parser.Received(1).ParseHelmConfigAsync(
            Arg.Is<string>(directory => directory.EndsWith(Path.Combine("v1.0.0", "charts/orders"))));
    }

    [Fact]
    public void ModuleNotParsable_SetsStateAndMessage()
    {
        var result = HelmValidationResult.ModuleNotParsable("bad yaml");

        Assert.Equal(HelmValidationResultState.ModuleNotParsable, result.State);
        Assert.Equal("bad yaml", result.Message);
        Assert.Null(result.Config);
    }

    private HelmValidator CreateValidator() => new(NullLogger<HelmValidator>.Instance, _git, _parser);

    private RunInput Input(RunInputProvider provider = RunInputProvider.Helm,
        IReadOnlyDictionary<string, string>? parameters = null, string? folderPath = null) =>
        new("chart", _templateName, "helm-chart", provider,
            new RunInputSource(new Uri("https://example.com/charts.git"), "v1.0.0", folderPath),
            parameters ?? new Dictionary<string, string>());
}
