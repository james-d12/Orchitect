namespace Orchitect.Infrastructure.Core.Encryption;

public sealed record EncryptionOptions
{
    public required string Key { get; init; }
}
