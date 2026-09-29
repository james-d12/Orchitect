using System.Text.RegularExpressions;

namespace Orchitect.Infrastructure.Inventory.Unit.Tests;

public sealed class NamespaceUsageTests
{
    private const string InventoryNamespace = "Orchitect.Infrastructure.Inventory";

    private static readonly string[] AllowedNamespaces =
    [
        $"{InventoryNamespace}.Shared"
    ];

    private static string GetRootPath()
    {
        var baseDir = AppDomain.CurrentDomain.BaseDirectory;
        var solutionDir =
            Directory.GetParent(baseDir)?.Parent?.Parent?.Parent?.Parent?.FullName ?? string.Empty;
        return Path.Combine(solutionDir, "Orchitect.Infrastructure.Inventory");
    }

    [Theory]
    [InlineData("Azure")]
    [InlineData("AzureDevOps")]
    [InlineData("GitHub")]
    [InlineData("GitLab")]
    public void UsingStatements_ShouldOnlyAllowThirdPartySelfAndSharedUsings(string moduleName)
    {
        var rootPath = GetRootPath();
        var modulePath = Path.Combine(rootPath, moduleName);
        var files = Directory.GetFiles(modulePath, "*.cs", SearchOption.AllDirectories);
        Assert.NotEmpty(files);

        foreach (var file in files)
        {
            var content = File.ReadAllText(file);
            var usings = Regex.Matches(content, @"^\s*using\s+([\w\.]+);", RegexOptions.Multiline)
                .Select(m => m.Groups[1].Value)
                .ToList();

            foreach (var usedNamespace in usings)
            {
                if (!usedNamespace.StartsWith($"{InventoryNamespace}.")) continue;

                var isAllowed = IsNamespaceOrChild(usedNamespace, $"{InventoryNamespace}.{moduleName}") ||
                                AllowedNamespaces.Any(n => IsNamespaceOrChild(usedNamespace, n));
                Assert.True(isAllowed, $"Disallowed namespace '{usedNamespace}' found in file '{file}'");
            }
        }
    }

    private static bool IsNamespaceOrChild(string usedNamespace, string parent) =>
        usedNamespace == parent || usedNamespace.StartsWith($"{parent}.");

    [Fact]
    public void IsNamespaceOrChild_DoesNotMatchSiblingWithSamePrefix()
    {
        Assert.True(IsNamespaceOrChild($"{InventoryNamespace}.Azure", $"{InventoryNamespace}.Azure"));
        Assert.True(IsNamespaceOrChild($"{InventoryNamespace}.Azure.Models", $"{InventoryNamespace}.Azure"));
        Assert.False(IsNamespaceOrChild($"{InventoryNamespace}.AzureDevOps", $"{InventoryNamespace}.Azure"));
    }
}
