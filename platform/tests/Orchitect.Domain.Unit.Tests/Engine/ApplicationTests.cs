using Orchitect.Domain.Core.Organisation;
using Orchitect.Domain.Engine.Application;

namespace Orchitect.Domain.Unit.Tests.Engine;

public sealed class ApplicationTests
{
    [Fact]
    public void Create_ValidRepository_Creates()
    {
        var application = Application.Create("orders", NewRepository("https://github.com/acme/orders.git"),
            new OrganisationId());

        Assert.Equal(new Uri("https://github.com/acme/orders.git"), application.Repository.Url);
    }

    [Theory]
    [InlineData("ftp://github.com/acme/orders.git")]
    [InlineData("https://github.com")]
    public void Create_InvalidRepositoryUrl_Throws(string url)
    {
        Assert.Throws<ArgumentException>(() =>
            Application.Create("orders", NewRepository(url), new OrganisationId()));
    }

    [Fact]
    public void Create_Request_InvalidRepositoryUrl_Throws()
    {
        var request = new CreateApplicationRequest("orders", Guid.NewGuid().ToString(),
            new CreateRepositoryRequest("orders", new Uri("ftp://github.com/acme/orders.git"),
                RepositoryProvider.GitHub));

        Assert.Throws<ArgumentException>(() => Application.Create(request));
    }

    [Fact]
    public void Create_EmptyRepositoryName_Throws()
    {
        var repository = NewRepository("https://github.com/acme/orders.git") with { Name = string.Empty };

        Assert.Throws<ArgumentException>(() => Application.Create("orders", repository, new OrganisationId()));
    }

    [Fact]
    public void Update_InvalidRepositoryUrl_Throws()
    {
        var application = Application.Create("orders", NewRepository("https://github.com/acme/orders.git"),
            new OrganisationId());

        Assert.Throws<ArgumentException>(() =>
            application.Update("orders", NewRepository("https://github.com/acme/orders?ref=main")));
    }

    private static Repository NewRepository(string url) => new()
    {
        Name = "orders",
        Url = new Uri(url),
        Provider = RepositoryProvider.GitHub
    };
}
