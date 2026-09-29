using Orchitect.Domain.Engine.ResourceTemplate;

namespace Orchitect.Engine.Execution.Provisioner;

public interface IProvisioner
{
    ResourceTemplateProvider Provider { get; }

    Task ProvisionAsync(
        List<ProvisionInput> inputs,
        ProvisionContext context,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(
        List<ProvisionInput> inputs,
        ProvisionContext context,
        CancellationToken cancellationToken = default);
}
