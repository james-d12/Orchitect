using Orchitect.Domain.Engine.ResourceTemplate;

namespace Orchitect.Infrastructure.Engine.Shared;

public interface IProvisioner
{
    ResourceTemplateProvider Provider { get; }

    Task ProvisionAsync(
        List<ProvisionInput> inputs,
        string folderName,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(
        List<ProvisionInput> inputs,
        string folderName,
        CancellationToken cancellationToken = default);
}