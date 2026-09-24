using Azure.Core;
using Azure.Identity;

namespace Orchitect.Infrastructure.Engine.Secret.Azure;

internal static class AzureCredentialFactory
{
    public static TokenCredential Create() => new DefaultAzureCredential();
}
