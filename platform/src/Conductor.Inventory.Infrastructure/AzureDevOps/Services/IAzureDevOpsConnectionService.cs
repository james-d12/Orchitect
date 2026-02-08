using Microsoft.VisualStudio.Services.WebApi;

namespace Conductor.Inventory.Infrastructure.AzureDevOps.Services;

public interface IAzureDevOpsConnectionService
{
    Task<T> GetClientAsync<T>(CancellationToken cancellationToken) where T : IVssHttpClient;
}