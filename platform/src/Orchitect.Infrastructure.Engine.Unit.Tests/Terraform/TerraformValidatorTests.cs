using Microsoft.Extensions.Logging.Abstractions;
using Orchitect.Domain.Core.Organisation;
using Orchitect.Domain.Engine.ResourceTemplate;
using Orchitect.Infrastructure.Engine.Provisioner.Terraform;
using Orchitect.Infrastructure.Engine.Provisioner.Terraform.Models;
using Orchitect.Infrastructure.Engine.Shared.CommandLine;

namespace Orchitect.Infrastructure.Engine.Unit.Tests.Terraform;

public sealed class TerraformValidatorTests : IDisposable
{
    private readonly string _cacheRoot = Path.Combine(Path.GetTempPath(), "orchitect-tests", Guid.NewGuid().ToString());

    public void Dispose()
    {
        if (Directory.Exists(_cacheRoot))
        {
            Directory.Delete(_cacheRoot, recursive: true);
        }
    }

    [Fact]
    public async Task ValidateAsync_InputsShareATemplate_DownloadsAndInspectsOnce()
    {
        var git = new TerraformModuleDownloaderTests.FakeGitCommandLine();
        var commandLine = new InspectingTerraformCommandLine();
        var validator = CreateValidator(git, commandLine);
        var template = Template(ResourceTemplateProvider.Terraform);
        List<TerraformPlanInput> inputs =
        [
            new(template, new Dictionary<string, string> { ["name"] = "payments" }, "paymentstorage"),
            new(template, new Dictionary<string, string> { ["name"] = "other" }, "otherpaymentstorage")
        ];

        var results = await validator.ValidateAsync(inputs);

        Assert.Equal(1, git.CloneCount);
        Assert.Equal(1, commandLine.InspectCount);
        Assert.All(results.Values, result => Assert.IsType<TerraformValidationResult.ValidResult>(result));
    }

    [Fact]
    public async Task ValidateAsync_WrongProvider_IsTemplateInvalidWithoutDownloading()
    {
        var git = new TerraformModuleDownloaderTests.FakeGitCommandLine();
        var validator = CreateValidator(git, new InspectingTerraformCommandLine());
        var input = new TerraformPlanInput(Template(ResourceTemplateProvider.Helm),
            new Dictionary<string, string> { ["name"] = "payments" }, "paymentstorage");

        var results = await validator.ValidateAsync([input]);

        Assert.Equal(TerraformValidationResult.ValidationResultState.TemplateInvalid, results[input].State);
        Assert.Equal(0, git.CloneCount);
    }

    [Fact]
    public async Task ValidateAsync_UnknownInput_IsInputInvalid()
    {
        var validator = CreateValidator(new TerraformModuleDownloaderTests.FakeGitCommandLine(),
            new InspectingTerraformCommandLine());
        var input = new TerraformPlanInput(Template(ResourceTemplateProvider.Terraform),
            new Dictionary<string, string> { ["name"] = "payments", ["sku"] = "premium" }, "paymentstorage");

        var results = await validator.ValidateAsync([input]);

        Assert.Equal(TerraformValidationResult.ValidationResultState.InputInvalid, results[input].State);
        Assert.Contains("sku", results[input].Message);
    }

    private TerraformValidator CreateValidator(IGitCommandLine git, ITerraformCommandLine commandLine) =>
        new(NullLogger<TerraformValidator>.Instance,
            new TerraformModuleDownloader(NullLogger<TerraformModuleDownloader>.Instance, git, _cacheRoot),
            commandLine);

    private static ResourceTemplate Template(ResourceTemplateProvider provider) =>
        ResourceTemplate.CreateWithVersion(new CreateResourceTemplateWithVersionRequest
        {
            OrganisationId = new OrganisationId(),
            Name = "Azure Storage Account",
            Type = "azure-storage-account",
            Description = "A storage account.",
            Provider = provider,
            Version = "1.0.0",
            Source = new ResourceTemplateVersionSource
            {
                BaseUrl = new Uri("https://example.com/storage.git"),
                Tag = string.Empty,
                FolderPath = string.Empty
            },
            Notes = string.Empty,
            State = ResourceTemplateVersionState.Active
        });

    private sealed class InspectingTerraformCommandLine : ITerraformCommandLine
    {
        private const string ModuleJson =
            """{"variables":{"name":{"name":"name","type":"string","required":true}}}""";

        private int _inspectCount;

        public int InspectCount => _inspectCount;

        public Task<CommandLineResult> RunTerraformJsonOutput(string executeDirectory)
        {
            Interlocked.Increment(ref _inspectCount);
            return Task.FromResult(new CommandLineResult(ModuleJson, string.Empty, 0));
        }

        public Task<CommandLineResult> RunInitAsync(string executeDirectory,
            IReadOnlyDictionary<string, string> backendConfig) => throw new NotSupportedException();

        public Task<CommandLineResult> RunValidateAsync(string executeDirectory) =>
            throw new NotSupportedException();

        public Task<CommandLineResult> RunPlanDestroyAsync(string executeDirectory, string planFileOutput) =>
            throw new NotSupportedException();

        public Task<CommandLineResult> RunPlanAsync(string executeDirectory, string planFileOutput) =>
            throw new NotSupportedException();

        public Task<CommandLineResult> RunApplyAsync(string executeDirectory, string planFile) =>
            throw new NotSupportedException();

        public Task<CommandLineResult> RunDestroyAsync(string executeDirectory, string planFile) =>
            throw new NotSupportedException();
    }
}
