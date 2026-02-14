namespace Orchitect.Core.Persistence.Services;

public sealed record EncryptionOptions
{
    public required string Key { get; init; }
}
