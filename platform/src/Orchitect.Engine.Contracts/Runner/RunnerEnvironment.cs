using Orchitect.Engine.Contracts.Secret;
using Orchitect.Engine.Contracts.Secret.Azure;

namespace Orchitect.Engine.Contracts.Runner;

public static class RunnerEnvironment
{
    private const string KeyVaultPrefix = $"{SecretProviderOptions.SectionName}__AzureKeyVault__";

    public const string RunId = "ORCHITECT_RUN_ID";
    public const string ConnectionString = "ConnectionStrings__orchitect";
    public const string KeyVaultAccessToken = $"{KeyVaultPrefix}{nameof(AzureKeyVaultOptions.AccessToken)}";
    public const string KeyVaultAccessTokenExpiresOn =
        $"{KeyVaultPrefix}{nameof(AzureKeyVaultOptions.AccessTokenExpiresOn)}";
}
