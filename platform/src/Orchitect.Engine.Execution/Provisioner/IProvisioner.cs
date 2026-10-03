using Orchitect.Engine.Contracts.Runner.Api;

namespace Orchitect.Engine.Execution.Provisioner;

public interface IProvisioner
{
    RunInputProvider Provider { get; }

    Task ProvisionAsync(
        List<RunInput> inputs,
        RunContext context,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(
        List<RunInput> inputs,
        RunContext context,
        CancellationToken cancellationToken = default);
}
