using System.Buffers.Text;
using Orchitect.Engine.Dispatch.Auth;

namespace Orchitect.Engine.Dispatch.Unit.Tests.Auth;

public sealed class RunnerTokenTests
{
    [Fact]
    public void Generate_Returns256BitValueAndItsHash()
    {
        var token = RunnerToken.Generate();

        Assert.Equal(32, Base64Url.DecodeFromChars(token.Value).Length);
        Assert.Equal(RunnerToken.ComputeHash(token.Value), token.Hash);
        Assert.Equal(64, token.Hash.Length);
    }

    [Fact]
    public void Generate_EachTokenIsDifferent()
    {
        Assert.NotEqual(RunnerToken.Generate().Value, RunnerToken.Generate().Value);
    }

    [Fact]
    public void ComputeHash_IsSha256Hex()
    {
        Assert.Equal("ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad", RunnerToken.ComputeHash("abc"));
    }

    [Fact]
    public void ToString_DoesNotContainValue()
    {
        var token = RunnerToken.Generate();

        Assert.DoesNotContain(token.Value, token.ToString(), StringComparison.Ordinal);
    }
}
