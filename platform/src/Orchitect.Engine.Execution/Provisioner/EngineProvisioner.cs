using Orchitect.Engine.Contracts.Runner.Api;

namespace Orchitect.Engine.Execution.Provisioner;

public interface IEngineProvisioner
{
    Task ProvisionAsync(
        IReadOnlyList<RunInput> inputs,
        RunContext context,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(
        IReadOnlyList<RunInput> inputs,
        RunContext context,
        CancellationToken cancellationToken = default);
}

public sealed class EngineProvisioner : IEngineProvisioner
{
    private readonly Dictionary<RunInputProvider, IProvisioner>
        _provisioners;

    public EngineProvisioner(
        IEnumerable<IProvisioner> provisioners)
    {
        _provisioners = provisioners.ToDictionary(x => x.Provider);
    }

    public async Task ProvisionAsync(
        IReadOnlyList<RunInput> inputs,
        RunContext context,
        CancellationToken cancellationToken = default)
    {
        foreach (var group in inputs.GroupBy(x => x.Provider))
        {
            await GetProvisioner(group.Key).ProvisionAsync(group.ToList(), context, cancellationToken);
        }
    }

    public async Task DeleteAsync(
        IReadOnlyList<RunInput> inputs,
        RunContext context,
        CancellationToken cancellationToken = default)
    {
        foreach (var group in inputs.GroupBy(x => x.Provider))
        {
            await GetProvisioner(group.Key).DeleteAsync(group.ToList(), context, cancellationToken);
        }
    }

    private IProvisioner GetProvisioner(RunInputProvider provider)
    {
        if (!_provisioners.TryGetValue(provider, out var provisioner))
        {
            throw new InvalidOperationException(
                $"No provisioner for provider {provider}");
        }

        return provisioner;
    }
}
