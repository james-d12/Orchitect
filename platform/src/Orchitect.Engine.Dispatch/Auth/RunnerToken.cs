using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;

namespace Orchitect.Engine.Dispatch.Auth;

public sealed record RunnerToken(string Value, string Hash)
{
    private const int SizeInBytes = 32;

    public static RunnerToken Generate()
    {
        var value = Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(SizeInBytes));
        return new RunnerToken(value, ComputeHash(value));
    }

    public static string ComputeHash(string value) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    public override string ToString() => $"{nameof(RunnerToken)} {{ {nameof(Hash)} = {Hash} }}";
}
