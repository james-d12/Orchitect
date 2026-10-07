namespace Orchitect.Storage.Unit.Tests;

public sealed class StorageKeyTests
{
    [Theory]
    [InlineData("plan.tfplan")]
    [InlineData("runs/0f8fad5b/log/terraform-init.log")]
    [InlineData("runs/.hidden/file")]
    public void Create_RelativePath_KeepsTheValue(string value)
    {
        Assert.Equal(value, StorageKey.Create(value).Value);
    }

    [Theory]
    [InlineData("")]
    [InlineData("/runs/a")]
    [InlineData("runs/a/")]
    [InlineData("runs//a")]
    [InlineData("../runs")]
    [InlineData("runs/../../etc/passwd")]
    [InlineData("runs/./a")]
    [InlineData("runs\\a")]
    [InlineData("runs/a\u0000b")]
    [InlineData("runs/a\nb")]
    public void Create_InvalidPath_Throws(string value)
    {
        Assert.Throws<ArgumentException>(() => StorageKey.Create(value));
    }

    [Fact]
    public void Combine_AppendsSegments()
    {
        var key = StorageKey.Create("runs/abc").Combine("log", "terraform-plan.log");

        Assert.Equal("runs/abc/log/terraform-plan.log", key.ToString());
    }

    [Fact]
    public void Combine_InvalidSegment_Throws()
    {
        Assert.Throws<ArgumentException>(() => StorageKey.Create("runs/abc").Combine(".."));
    }

    [Fact]
    public void Equality_ComparesValues()
    {
        Assert.Equal(StorageKey.Create("runs/abc"), StorageKey.Create("runs").Combine("abc"));
    }
}
