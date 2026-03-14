using Orchitect.Infrastructure.Inventory.AzureDevOps.Models;

namespace Orchitect.Infrastructure.Inventory.AzureDevOps.Services;

public interface IAzureDevOpsQueryService
{
    AzureDevOpsRepository? GetRepository(string repositoryName);
    AzureDevOpsPipeline? GetPipeline(string pipelineName);
}