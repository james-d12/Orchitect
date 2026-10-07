namespace Orchitect.Storage.Azure;

public sealed record AzureBlobStorageOptions
{
    private const string ConfigPath = $"{StorageOptions.SectionName}:AzureBlob";
    private const string EnvironmentPrefix = $"{StorageOptions.SectionName}__AzureBlob__";

    public const string AccessTokenEnvironmentKey = $"{EnvironmentPrefix}{nameof(AccessToken)}";
    public const string AccessTokenExpiresOnEnvironmentKey = $"{EnvironmentPrefix}{nameof(AccessTokenExpiresOn)}";

    public Uri? ContainerUri { get; init; }

    public string? AccessToken { get; init; }

    public DateTimeOffset? AccessTokenExpiresOn { get; init; }

    public string? GetValidationError() => ContainerUri switch
    {
        null => $"{ConfigPath}:ContainerUri is required when {StorageOptions.SectionName}:Type is " +
                $"{StorageProviderType.AzureBlob}.",
        { IsAbsoluteUri: false } => $"{ConfigPath}:ContainerUri '{ContainerUri}' must be an absolute URI.",
        { Query.Length: > 0 } => $"{ConfigPath}:ContainerUri must not carry a query string. Use an access token " +
                                 "or a managed identity instead of a SAS URL.",
        _ => null
    };
}
