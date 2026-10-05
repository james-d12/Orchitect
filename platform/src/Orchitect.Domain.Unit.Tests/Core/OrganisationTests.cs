using Orchitect.Domain.Core.Organisation;

namespace Orchitect.Domain.Unit.Tests.Core;

public sealed class OrganisationTests
{
    [Fact]
    public void AddUser_ExistingUser_Throws()
    {
        var organisation = Organisation.Create("acme").AddUser("user-1");

        Assert.Throws<InvalidOperationException>(() => organisation.AddUser("user-1"));
    }

    [Fact]
    public void RemoveUser_LastMember_Throws()
    {
        var organisation = Organisation.Create("acme").AddUser("user-1");

        Assert.Throws<InvalidOperationException>(() => organisation.RemoveUser(organisation.Users[0].Id));
    }

    [Fact]
    public void RemoveUser_OneOfSeveralMembers_RemovesThem()
    {
        var organisation = Organisation.Create("acme").AddUser("user-1").AddUser("user-2");

        var updated = organisation.RemoveUser(organisation.Users[0].Id);

        Assert.Equal("user-2", Assert.Single(updated.Users).IdentityUserId);
    }
}
