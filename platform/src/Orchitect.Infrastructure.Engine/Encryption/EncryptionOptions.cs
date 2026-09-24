namespace Orchitect.Infrastructure.Engine.Encryption;

public sealed record EncryptionOptions
{
    public const string SectionName = "EncryptionOptions";
    public required string Key { get; init; }
}
