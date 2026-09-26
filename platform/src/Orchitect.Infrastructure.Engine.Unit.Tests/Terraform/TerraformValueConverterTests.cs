using System.Text.Json.Nodes;
using Orchitect.Infrastructure.Engine.Provisioner.Terraform;

namespace Orchitect.Infrastructure.Engine.Unit.Tests.Terraform;

public sealed class TerraformValueConverterTests
{
    [Theory]
    [InlineData("true", "string", "\"true\"")]
    [InlineData("${file(\"/etc/passwd\")}", "string", "\"${file(\\u0022/etc/passwd\\u0022)}\"")]
    [InlineData("1.5", "number", "1.5")]
    [InlineData("False", "bool", "false")]
    [InlineData("['a','b']", "list(string)", "[\"a\",\"b\"]")]
    [InlineData("[1,2]", "set(number)", "[1,2]")]
    [InlineData("{\"a\":1}", "object({a=number})", "{\"a\":1}")]
    [InlineData("true", null, "true")]
    [InlineData("12", "any", "12")]
    [InlineData("[\"x\"]", null, "[\"x\"]")]
    [InlineData("[not json", null, "\"[not json\"")]
    [InlineData("hello", null, "\"hello\"")]
    [InlineData("7", "", "7")]
    [InlineData("7", "  ", "7")]
    public void TryConvert_ValidValue_ReturnsTypedJson(string raw, string? type, string expected)
    {
        Assert.True(TerraformValueConverter.TryConvert(raw, type, out var value, out _));
        Assert.Equal(expected, value!.ToJsonString());
    }

    [Theory]
    [InlineData("abc", "number")]
    [InlineData("yes", "bool")]
    [InlineData("a,b", "list(string)")]
    [InlineData("{\"a\":1}", "list(string)")]
    [InlineData("[1]", "map(string)")]
    public void TryConvert_ValueDoesNotMatchType_ReturnsError(string raw, string type)
    {
        Assert.False(TerraformValueConverter.TryConvert(raw, type, out JsonNode? value, out var error));
        Assert.Null(value);
        Assert.Contains(raw, error);
    }
}
