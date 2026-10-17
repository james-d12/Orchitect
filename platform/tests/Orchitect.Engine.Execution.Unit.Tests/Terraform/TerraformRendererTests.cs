using Orchitect.Engine.Contracts.Runner.Api;
using System.Text.Json.Nodes;
using Orchitect.Engine.Execution.Provisioner.Terraform;
using Orchitect.Engine.Execution.Provisioner.Terraform.Models;

namespace Orchitect.Engine.Execution.Unit.Tests.Terraform;

public sealed class TerraformRendererTests
{
    private const string InjectionPayload =
        "x\"\n}\nresource \"null_resource\" \"pwn\" {\n  provisioner \"local-exec\" { command = \"id\" }\n}\n# ${file(\"/proc/self/environ\")} %{ if true }";

    [Theory]
    [InlineData("azurerm")]
    [InlineData("s3")]
    [InlineData("gcs")]
    public void RenderBackend_RendersPartialBackendBlockForType(string backendType)
    {
        var backend = JsonNode.Parse(new TerraformRenderer().RenderBackend(backendType))!;

        Assert.Empty(backend["terraform"]!["backend"]![backendType]!.AsObject());
    }

    [Fact]
    public void RenderModules_InputValues_OnlyAppearVerbatimInTfVars()
    {
        var plans = Plans(new Dictionary<string, string> { ["name"] = InjectionPayload },
            ("name", "string"));

        var rendered = new TerraformRenderer().RenderModules(plans);

        var main = JsonNode.Parse(rendered.MainTfJson)!;
        var tfVars = JsonNode.Parse(rendered.TfVarsJson)!;
        Assert.Equal(InjectionPayload, tfVars["storage_account_storage__name"]!.GetValue<string>());
        Assert.Equal("${var.storage_account_storage__name}",
            main["module"]!["storage_account_storage"]!["name"]!.GetValue<string>());
        Assert.Equal("string", main["variable"]!["storage_account_storage__name"]!["type"]!.GetValue<string>());
        Assert.DoesNotContain("proc/self/environ", rendered.MainTfJson);
        Assert.DoesNotContain("null_resource", rendered.MainTfJson);
    }

    [Fact]
    public void RenderModules_TypedVariables_RendersTypedTfVars()
    {
        var plans = Plans(new Dictionary<string, string>
        {
            ["replicas"] = "3",
            ["enabled"] = "true",
            ["zones"] = "['1','2']",
            ["tags"] = """{"team":"payments"}""",
            ["untyped"] = "42"
        },
            ("replicas", "number"), ("enabled", "bool"), ("zones", "list(string)"), ("tags", "map(string)"),
            ("untyped", null));

        var tfVars = JsonNode.Parse(new TerraformRenderer().RenderModules(plans).TfVarsJson)!;

        Assert.Equal(3m, tfVars["storage_account_storage__replicas"]!.GetValue<decimal>());
        Assert.True(tfVars["storage_account_storage__enabled"]!.GetValue<bool>());
        Assert.Equal(["1", "2"],
            tfVars["storage_account_storage__zones"]!.AsArray().Select(z => z!.GetValue<string>()));
        Assert.Equal("payments", tfVars["storage_account_storage__tags"]!["team"]!.GetValue<string>());
        Assert.Equal(42m, tfVars["storage_account_storage__untyped"]!.GetValue<decimal>());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void RenderModules_UntypedVariable_DeclaresVariableWithoutType(string? type)
    {
        var plans = Plans(new Dictionary<string, string> { ["name"] = "value" }, ("name", type));

        var main = JsonNode.Parse(new TerraformRenderer().RenderModules(plans).MainTfJson)!;

        Assert.Empty(main["variable"]!["storage_account_storage__name"]!.AsObject());
    }

    [Fact]
    public void RenderModules_ModuleName_IsSanitisedToAnIdentifier()
    {
        var input = TerraformTestData.PlanInput() with { Key = "9 bad\"key}" };
        var plans = new Dictionary<RunInput, TerraformValidationResult.ValidResult>
        {
            [input] = TerraformTestData.ValidResult()
        };

        var main = JsonNode.Parse(new TerraformRenderer().RenderModules(plans).MainTfJson)!;

        Assert.Equal(["storage_account_9_bad_key_"], main["module"]!.AsObject().Select(m => m.Key));
    }

    [Fact]
    public void RenderModules_ParameterIsReference_RendersModuleOutputExpression()
    {
        var plans = ReferencePlans("${resources.vault.id}");

        var rendered = new TerraformRenderer().RenderModules(plans);

        var main = JsonNode.Parse(rendered.MainTfJson)!;
        Assert.Equal("${module.key_vault_vault.id}",
            main["module"]!["storage_account_storage"]!["key_vault_id"]!.GetValue<string>());
        Assert.Null(main["variable"]?["storage_account_storage__key_vault_id"]);
        Assert.Null(JsonNode.Parse(rendered.TfVarsJson)!["storage_account_storage__key_vault_id"]);
    }

    [Fact]
    public void RenderModules_ReferenceInsideText_EscapesTheLiteralText()
    {
        var plans = ReferencePlans("${resources.vault.name}/${file(\"/etc/passwd\")}%{ if true }");

        var main = JsonNode.Parse(new TerraformRenderer().RenderModules(plans).MainTfJson)!;

        Assert.Equal("${module.key_vault_vault.name}/$${file(\"/etc/passwd\")}%%{ if true }",
            main["module"]!["storage_account_storage"]!["key_vault_id"]!.GetValue<string>());
    }

    [Fact]
    public void RenderModules_ReferenceToResourceOutsideTheRun_Throws()
    {
        var plans = Plans(new Dictionary<string, string> { ["key_vault_id"] = "${resources.vault.id}" },
            ("key_vault_id", "string"));

        var exception = Assert.Throws<InvalidOperationException>(() => new TerraformRenderer().RenderModules(plans));

        Assert.Contains("vault", exception.Message);
    }

    [Fact]
    public void RenderProviders_RendersRequiredProvidersAndProviderBlocks()
    {
        var providers = JsonNode.Parse(new TerraformRenderer().RenderProviders(
            [new TerraformProvider("azurerm", "hashicorp/azurerm", "~> 4.0")]))!;

        var requirement = providers["terraform"]!["required_providers"]!["azurerm"]!;
        Assert.Equal("hashicorp/azurerm", requirement["source"]!.GetValue<string>());
        Assert.Equal("~> 4.0", requirement["version"]!.GetValue<string>());
        Assert.NotNull(providers["provider"]!["azurerm"]!["features"]);
    }

    [Fact]
    public void RenderBackendConfig_EscapesValuesSoTheyStayLiteral()
    {
        var rendered = new TerraformRenderer().RenderBackendConfig(new Dictionary<string, string>
        {
            ["key"] = "a\"b\\c\n${file(\"/etc/passwd\")}%{if true}x%{endif}",
            ["use_azuread_auth"] = "true"
        });

        Assert.Equal(
            "key = \"a\\\"b\\\\c\\n$${file(\\\"/etc/passwd\\\")}%%{if true}x%%{endif}\"\n" +
            "use_azuread_auth = \"true\"\n",
            rendered);
    }

    [Theory]
    [InlineData("key\nresource")]
    [InlineData("key = \"x\"")]
    [InlineData("1key")]
    [InlineData("")]
    public void RenderBackendConfig_InvalidKey_Throws(string key)
    {
        Assert.Throws<InvalidOperationException>(() =>
            new TerraformRenderer().RenderBackendConfig(new Dictionary<string, string> { [key] = "value" }));
    }

    private static Dictionary<RunInput, TerraformValidationResult.ValidResult> ReferencePlans(string value)
    {
        var plans = Plans(new Dictionary<string, string> { ["key_vault_id"] = value }, ("key_vault_id", "string"));
        plans[TerraformTestData.Input(key: "vault", templateName: "Key Vault")] =
            TerraformValidationResult.Valid(new TerraformConfig(), "/modules/vault");
        return plans;
    }

    private static Dictionary<RunInput, TerraformValidationResult.ValidResult> Plans(
        Dictionary<string, string> inputs, params (string Name, string? Type)[] variables)
    {
        var config = new TerraformConfig
        {
            Variables = variables.ToDictionary(v => v.Name,
                v => new TerraformConfig.Variable { Name = v.Name, Type = v.Type })
        };

        return new Dictionary<RunInput, TerraformValidationResult.ValidResult>
        {
            [TerraformTestData.PlanInput() with { Parameters = inputs }] =
                TerraformValidationResult.Valid(config, "/modules/storage")
        };
    }
}
