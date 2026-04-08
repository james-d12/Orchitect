using Orchitect.Domain.Inventory.Cloud.Requests;

namespace Orchitect.Domain.Inventory.Cloud.Services;

public interface ICloudQueryService
{
    List<CloudResource> QueryCloudResources(CloudResourceQueryRequest request);
    List<CloudSecret> QueryCloudSecrets(CloudSecretQueryRequest request);
}