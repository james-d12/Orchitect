using Orchitect.Domain.Inventory.Cloud.Request;

namespace Orchitect.Domain.Inventory.Cloud.Service;

public interface ICloudQueryService
{
    List<CloudResource> QueryCloudResources(CloudResourceQueryRequest request);
    List<CloudSecret> QueryCloudSecrets(CloudSecretQueryRequest request);
}