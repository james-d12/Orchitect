using Orchitect.Domain.Engine.Git;

namespace Orchitect.Domain.Unit.Tests.Engine;

public sealed class GitValidatorTests
{
    [Theory]
    [InlineData("https://github.com/acme/orders.git")]
    [InlineData("https://github.com/acme/orders")]
    [InlineData("http://git.internal/acme/orders.git")]
    [InlineData("ssh://git@github.com/acme/orders.git")]
    [InlineData("git://github.com/acme/orders.git")]
    [InlineData("https://dev.azure.com/acme/project/_git/orders")]
    public void IsValidRepositoryUrl_GitUrl_ReturnsTrue(string url)
    {
        Assert.True(GitValidator.IsValidRepositoryUrl(new Uri(url)));
    }

    [Theory]
    [InlineData("ftp://github.com/acme/orders.git")]
    [InlineData("file:///srv/git/orders.git")]
    [InlineData("mailto:dev@acme.com")]
    [InlineData("https://github.com")]
    [InlineData("https://github.com/")]
    [InlineData("https://github.com/acme/orders?ref=main")]
    [InlineData("https://github.com/acme/orders#readme")]
    [InlineData("https://user:secret@github.com/acme/orders.git")]
    public void IsValidRepositoryUrl_NotGitUrl_ReturnsFalse(string url)
    {
        Assert.False(GitValidator.IsValidRepositoryUrl(new Uri(url)));
    }

    [Fact]
    public void IsValidRepositoryUrl_RelativeOrNull_ReturnsFalse()
    {
        Assert.False(GitValidator.IsValidRepositoryUrl(new Uri("acme/orders", UriKind.Relative)));
        Assert.False(GitValidator.IsValidRepositoryUrl(null));
    }

    [Theory]
    [InlineData("a3ce136a19022106a23a9b8e56768dcf18523396")]
    [InlineData("A3CE136A19022106A23A9B8E56768DCF18523396")]
    [InlineData("5f2a0b8c9d1e3f4a5b6c7d8e9f0a1b2c3d4e5f6a7b8c9d0e1f2a3b4c5d6e7f8a")]
    public void IsValidCommitId_FullSha_ReturnsTrue(string commitId)
    {
        Assert.True(GitValidator.IsValidCommitId(commitId));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("abc123")]
    [InlineData("a3ce136")]
    [InlineData("a3ce136a19022106a23a9b8e56768dcf1852339")]
    [InlineData("a3ce136a19022106a23a9b8e56768dcf185233960")]
    [InlineData("g3ce136a19022106a23a9b8e56768dcf18523396")]
    [InlineData(" a3ce136a19022106a23a9b8e56768dcf18523396")]
    [InlineData("main")]
    public void IsValidCommitId_NotFullSha_ReturnsFalse(string? commitId)
    {
        Assert.False(GitValidator.IsValidCommitId(commitId));
    }

    [Theory]
    [InlineData("v1.2.3")]
    [InlineData("1.0.0")]
    [InlineData("release/2026-10")]
    [InlineData("feature/my_branch")]
    public void IsValidReference_ValidRef_ReturnsTrue(string reference)
    {
        Assert.True(GitValidator.IsValidReference(reference));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("@")]
    [InlineData("-v1")]
    [InlineData("/v1")]
    [InlineData("v1/")]
    [InlineData("v1.")]
    [InlineData("v1..2")]
    [InlineData("v1@{0}")]
    [InlineData("release//v1")]
    [InlineData("v 1")]
    [InlineData("v1~1")]
    [InlineData("v1^")]
    [InlineData("v1:2")]
    [InlineData("v1?")]
    [InlineData("v1*")]
    [InlineData("v1[")]
    [InlineData("v1\\2")]
    [InlineData("v1\t")]
    [InlineData(".hidden")]
    [InlineData("release/.hidden")]
    [InlineData("v1.lock")]
    [InlineData("v1.lock/v2")]
    public void IsValidReference_InvalidRef_ReturnsFalse(string? reference)
    {
        Assert.False(GitValidator.IsValidReference(reference));
    }
}
