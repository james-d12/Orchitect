using Microsoft.VisualStudio.Services.WebApi;

namespace Orchitect.Infrastructure.Inventory.AzureDevOps.Services;

public interface IAzureDevOpsConnectionService
{
    Task<T> GetClientAsync<T>(CancellationToken cancellationToken) where T : IVssHttpClient;
}