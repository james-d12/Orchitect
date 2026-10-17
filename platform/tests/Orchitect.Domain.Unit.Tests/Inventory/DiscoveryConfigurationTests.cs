using Orchitect.Domain.Core.Credential;
using Orchitect.Domain.Core.Organisation;
using Orchitect.Domain.Inventory.Discovery;

namespace Orchitect.Domain.Unit.Tests.Inventory;

public sealed class DiscoveryConfigurationTests
{
    [Fact]
    public void Create_ValidName_SetsName()
    {
        var configuration = NewConfiguration("Main GitHub Account");

        Assert.Equal("Main GitHub Account", configuration.Name);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_BlankName_Throws(string name)
    {
        Assert.Throws<ArgumentException>(() => NewConfiguration(name));
    }

    [Fact]
    public void Create_NameTooLong_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            NewConfiguration(new string('a', DiscoveryConfiguration.NameMaxLength + 1)));
    }

    [Fact]
    public void Update_ValidName_ChangesName()
    {
        var configuration = NewConfiguration("Main GitHub Account");

        var updated = configuration.Update("Secondary GitHub Account", isEnabled: true);

        Assert.Equal("Secondary GitHub Account", updated.Name);
    }

    [Fact]
    public void Update_BlankName_Throws()
    {
        var configuration = NewConfiguration("Main GitHub Account");

        Assert.Throws<ArgumentException>(() => configuration.Update(" ", isEnabled: true));
    }

    private static DiscoveryConfiguration NewConfiguration(string name) =>
        DiscoveryConfiguration.Create(new OrganisationId(), new CredentialId(), name, DiscoveryPlatform.GitHub);
}
