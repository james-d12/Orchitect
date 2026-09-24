namespace Orchitect.Infrastructure.Engine.Encryption;

public sealed record EncryptionOptions
{
    public required string Key { get; init; }
}
