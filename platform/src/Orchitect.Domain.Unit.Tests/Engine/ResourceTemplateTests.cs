using Orchitect.Domain.Core.Organisation;
using Orchitect.Domain.Engine.ResourceTemplate;

namespace Orchitect.Domain.Unit.Tests.Engine;

public sealed class ResourceTemplateTests
{
    [Theory]
    [InlineData("v1.2.3")]
    [InlineData("")]
    public void CreateWithVersion_ValidSource_AddsVersion(string tag)
    {
        var template = ResourceTemplate.CreateWithVersion(NewRequest("https://github.com/acme/storage.git", tag));

        Assert.Single(template.Versions);
    }

    [Theory]
    [InlineData("ftp://github.com/acme/storage.git")]
    [InlineData("https://github.com")]
    public void CreateWithVersion_InvalidSourceUrl_Throws(string url)
    {
        Assert.Throws<ArgumentException>(() => ResourceTemplate.CreateWithVersion(NewRequest(url, "v1.0.0")));
    }

    [Theory]
    [InlineData("v1..0")]
    [InlineData("-v1")]
    [InlineData("v 1")]
    public void CreateWithVersion_InvalidSourceTag_Throws(string tag)
    {
        Assert.Throws<ArgumentException>(() =>
            ResourceTemplate.CreateWithVersion(NewRequest("https://github.com/acme/storage.git", tag)));
    }

    [Fact]
    public void DeactivateVersion_OnlyVersion_LeavesNoLatestVersion()
    {
        var template = ResourceTemplate.CreateWithVersion(NewRequest("https://github.com/acme/storage.git", "v1"));

        template.DeactivateVersion(template.Versions[0].Id);

        Assert.Equal(ResourceTemplateVersionState.Inactive, template.Versions[0].State);
        Assert.Null(template.GetLatestVersion());
    }

    [Fact]
    public void DeactivateVersion_UnknownVersion_Throws()
    {
        var template = ResourceTemplate.CreateWithVersion(NewRequest("https://github.com/acme/storage.git", "v1"));

        Assert.Throws<InvalidOperationException>(() => template.DeactivateVersion(new ResourceTemplateVersionId()));
    }

    private static CreateResourceTemplateWithVersionRequest NewRequest(string url, string tag) => new()
    {
        OrganisationId = new OrganisationId(),
        Name = "storage",
        Type = "azure-storage-account",
        Description = "Storage account",
        Provider = ResourceTemplateProvider.Terraform,
        Version = "1.0.0",
        Source = new ResourceTemplateVersionSource
        {
            BaseUrl = new Uri(url),
            FolderPath = string.Empty,
            Tag = tag
        },
        Notes = string.Empty,
        State = ResourceTemplateVersionState.Active
    };
}
