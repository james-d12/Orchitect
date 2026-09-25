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

    [Fact]
    public async Task ValidateAsync_InputCaseDiffersFromVariable_IsInputInvalid()
    {
        var validator = CreateValidator(new TerraformModuleDownloaderTests.FakeGitCommandLine(),
            new InspectingTerraformCommandLine());
        var input = new TerraformPlanInput(Template(ResourceTemplateProvider.Terraform),
            new Dictionary<string, string> { ["Name"] = "payments" }, "paymentstorage");

        var results = await validator.ValidateAsync([input]);

        Assert.Equal(TerraformValidationResult.ValidationResultState.InputInvalid, results[input].State);
        Assert.Contains("Name", results[input].Message);
    }

    [Fact]
    public async Task ValidateAsync_ValueDoesNotMatchVariableType_IsInputInvalid()
    {
        const string moduleJson =
            """{"variables":{"replicas":{"name":"replicas","type":"number","required":true}}}""";
        var validator = CreateValidator(new TerraformModuleDownloaderTests.FakeGitCommandLine(),
            new InspectingTerraformCommandLine(_ => new CommandLineResult(moduleJson, string.Empty, 0)));
        var input = new TerraformPlanInput(Template(ResourceTemplateProvider.Terraform),
            new Dictionary<string, string> { ["replicas"] = "three" }, "paymentstorage");

        var results = await validator.ValidateAsync([input]);

        Assert.Equal(TerraformValidationResult.ValidationResultState.InputInvalid, results[input].State);
        Assert.Contains("replicas", results[input].Message);
    }

    [Fact]
    public async Task ValidateAsync_InspectReportsDiagnostics_IsModuleInvalidWithDiagnostic()
    {
        const string diagnosticsJson =
            """
            {"variables":{},"diagnostics":[{"severity":"error","summary":"Unclosed configuration block",
            "detail":"There is no closing brace for this block.","pos":{"filename":"main.tf","line":3}}]}
            """;
        var validator = CreateValidator(new TerraformModuleDownloaderTests.FakeGitCommandLine(),
            new InspectingTerraformCommandLine(_ => new CommandLineResult(diagnosticsJson, string.Empty, 1)));
        var input = new TerraformPlanInput(Template(ResourceTemplateProvider.Terraform),
            new Dictionary<string, string>(), "paymentstorage");

        var results = await validator.ValidateAsync([input]);

        Assert.Equal(TerraformValidationResult.ValidationResultState.ModuleInvalid, results[input].State);
        Assert.Contains("main.tf:3: Unclosed configuration block", results[input].Message);
    }

    [Fact]
    public async Task ValidateAsync_InspectReturnsMalformedJson_OnlyThatTemplateIsModuleInvalid()
    {
        var git = new TerraformModuleDownloaderTests.FakeGitCommandLine("broken");
        var commandLine = new InspectingTerraformCommandLine(directory => directory.EndsWith("broken")
            ? new CommandLineResult("not json", "boom", 1)
            : new CommandLineResult(
                """{"variables":{"name":{"name":"name","type":"string","required":true}}}""", string.Empty, 0));
        var validator = CreateValidator(git, commandLine);
        var broken = new TerraformPlanInput(Template(ResourceTemplateProvider.Terraform, "broken"),
            new Dictionary<string, string>(), "broken");
        var healthy = new TerraformPlanInput(Template(ResourceTemplateProvider.Terraform),
            new Dictionary<string, string> { ["name"] = "payments" }, "healthy");

        var results = await validator.ValidateAsync([broken, healthy]);

        Assert.Equal(TerraformValidationResult.ValidationResultState.ModuleInvalid, results[broken].State);
        Assert.Contains("boom", results[broken].Message);
        Assert.IsType<TerraformValidationResult.ValidResult>(results[healthy]);
    }

    [Fact]
    public async Task ValidateAsync_TfFilesOnlyInSubdirectory_IsModuleInvalid()
    {
        var validator = CreateValidator(new NestedOnlyGitCommandLine(), new InspectingTerraformCommandLine());
        var input = new TerraformPlanInput(Template(ResourceTemplateProvider.Terraform),
            new Dictionary<string, string> { ["name"] = "payments" }, "paymentstorage");

        var results = await validator.ValidateAsync([input]);

        Assert.Equal(TerraformValidationResult.ValidationResultState.ModuleInvalid, results[input].State);
    }

    private TerraformValidator CreateValidator(IGitCommandLine git, ITerraformCommandLine commandLine) =>
        new(NullLogger<TerraformValidator>.Instance,
            new TerraformModuleDownloader(NullLogger<TerraformModuleDownloader>.Instance, git, _cacheRoot),
            commandLine);

    private static ResourceTemplate Template(ResourceTemplateProvider provider, string folderPath = "") =>
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
                FolderPath = folderPath
            },
            Notes = string.Empty,
            State = ResourceTemplateVersionState.Active
        });

    private sealed class NestedOnlyGitCommandLine : IGitCommandLine
    {
        public Task<bool> CloneAsync(Uri source, string destination) => Clone(destination);

        public Task<bool> CloneTagAsync(Uri source, string tag, string destination) => Clone(destination);

        public Task<bool> CloneCommitAsync(Uri source, string commit, string destination) => Clone(destination);

        private static Task<bool> Clone(string destination)
        {
            var examples = Path.Combine(destination, "examples");
            Directory.CreateDirectory(examples);
            File.WriteAllText(Path.Combine(examples, "variables.tf"), string.Empty);
            File.WriteAllText(Path.Combine(examples, "outputs.tf"), string.Empty);
            return Task.FromResult(true);
        }
    }

    private sealed class InspectingTerraformCommandLine : ITerraformCommandLine
    {
        private const string ModuleJson =
            """{"variables":{"name":{"name":"name","type":"string","required":true}}}""";

        private readonly Func<string, CommandLineResult> _inspect;
        private int _inspectCount;

        public InspectingTerraformCommandLine(Func<string, CommandLineResult>? inspect = null)
        {
            _inspect = inspect ?? (_ => new CommandLineResult(ModuleJson, string.Empty, 0));
        }

        public int InspectCount => _inspectCount;

        public Task<CommandLineResult> RunTerraformJsonOutput(string executeDirectory)
        {
            Interlocked.Increment(ref _inspectCount);
            return Task.FromResult(_inspect(executeDirectory));
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
    }
}
