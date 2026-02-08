using Orchitect.Inventory.Domain.Cloud.Request;

namespace Orchitect.Inventory.Domain.Cloud.Service;

public interface ICloudQueryService
{
    List<CloudResource> QueryCloudResources(CloudResourceQueryRequest request);
    List<CloudSecret> QueryCloudSecrets(CloudSecretQueryRequest request);
}