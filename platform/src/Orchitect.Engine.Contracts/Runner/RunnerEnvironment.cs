using Orchitect.Engine.Contracts.Secret;
using Orchitect.Engine.Contracts.Secret.Azure;

namespace Orchitect.Engine.Contracts.Runner;

public static class RunnerEnvironment
{
    private const string KeyVaultPrefix = $"{SecretProviderOptions.SectionName}__AzureKeyVault__";

    public const string RunId = "ORCHITECT_RUN_ID";
    public const string RunToken = "ORCHITECT_RUN_TOKEN";
    public const string ApiBaseUrl = "ORCHITECT_API_URL";
    public const string KeyVaultAccessToken = $"{KeyVaultPrefix}{nameof(AzureKeyVaultOptions.AccessToken)}";
    public const string KeyVaultAccessTokenExpiresOn =
        $"{KeyVaultPrefix}{nameof(AzureKeyVaultOptions.AccessTokenExpiresOn)}";

    public const string TraceParent = "TRACEPARENT";
    public const string TraceState = "TRACESTATE";
    public const string OtlpEndpoint = "OTEL_EXPORTER_OTLP_ENDPOINT";
    public const string OtlpHeaders = "OTEL_EXPORTER_OTLP_HEADERS";
    public const string OtlpProtocol = "OTEL_EXPORTER_OTLP_PROTOCOL";
    public const string ServiceName = "OTEL_SERVICE_NAME";
}
