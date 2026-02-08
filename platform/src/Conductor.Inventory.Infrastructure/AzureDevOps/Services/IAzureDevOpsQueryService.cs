using Conductor.Inventory.Infrastructure.AzureDevOps.Models;

namespace Conductor.Inventory.Infrastructure.AzureDevOps.Services;

public interface IAzureDevOpsQueryService
{
    AzureDevOpsRepository? GetRepository(string repositoryName);
    AzureDevOpsPipeline? GetPipeline(string pipelineName);
}