using Microsoft.Extensions.Options;
using Microsoft.VisualStudio.Services.Common;
using Microsoft.VisualStudio.Services.WebApi;
using Orchitect.Inventory.Infrastructure.AzureDevOps.Models;

namespace Orchitect.Inventory.Infrastructure.AzureDevOps.Services;

public sealed class AzureDevOpsConnectionService : IAzureDevOpsConnectionService
{
    private readonly VssConnection _connection;

    public AzureDevOpsConnectionService(IOptions<AzureDevOpsSettings> options)
    {
        var connectionUri = new Uri($"https://dev.azure.com/{options.Value.Organization}");
        var credentials = new VssBasicCredential(string.Empty, options.Value.PersonalAccessToken);
        _connection = new VssConnection(connectionUri, credentials);
    }

    public Task<T> GetClientAsync<T>(CancellationToken cancellationToken) where T : IVssHttpClient
    {
        return _connection.GetClientAsync<T>(cancellationToken);
    }
}