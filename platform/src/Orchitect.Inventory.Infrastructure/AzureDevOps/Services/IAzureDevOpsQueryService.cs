using Orchitect.Inventory.Infrastructure.AzureDevOps.Models;

namespace Orchitect.Inventory.Infrastructure.AzureDevOps.Services;

public interface IAzureDevOpsQueryService
{
    AzureDevOpsRepository? GetRepository(string repositoryName);
    AzureDevOpsPipeline? GetPipeline(string pipelineName);
}