using Conductor.Inventory.Domain.Cloud.Request;

namespace Conductor.Inventory.Domain.Cloud.Service;

public interface ICloudQueryService
{
    List<CloudResource> QueryCloudResources(CloudResourceQueryRequest request);
    List<CloudSecret> QueryCloudSecrets(CloudSecretQueryRequest request);
}