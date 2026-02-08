using Microsoft.VisualStudio.Services.WebApi;

namespace Orchitect.Inventory.Infrastructure.AzureDevOps.Services;

public interface IAzureDevOpsConnectionService
{
    Task<T> GetClientAsync<T>(CancellationToken cancellationToken) where T : IVssHttpClient;
}