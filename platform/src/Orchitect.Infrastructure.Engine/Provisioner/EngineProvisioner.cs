using Orchitect.Domain.Engine.ResourceTemplate;

namespace Orchitect.Infrastructure.Engine.Provisioner;

public sealed record ProvisionInput(ResourceTemplate Template, Dictionary<string, string> Inputs, string Key);

public sealed record ProvisionContext(string ProjectName, string ApplicationId, string EnvironmentId);

public interface IEngineProvisioner
{
    Task ProvisionAsync(
        List<ProvisionInput> inputs,
        ProvisionContext context,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(
        List<ProvisionInput> inputs,
        ProvisionContext context,
        CancellationToken cancellationToken = default);
}

public sealed class EngineProvisioner : IEngineProvisioner
{
    private readonly Dictionary<ResourceTemplateProvider, IProvisioner>
        _provisioners;

    public EngineProvisioner(
        IEnumerable<IProvisioner> provisioners)
    {
        _provisioners = provisioners.ToDictionary(x => x.Provider);
    }

    public async Task ProvisionAsync(
        List<ProvisionInput> inputs,
        ProvisionContext context,
        CancellationToken cancellationToken = default)
    {
        foreach (var group in inputs.GroupBy(x => x.Template.Provider))
        {
            await GetProvisioner(group.Key).ProvisionAsync(group.ToList(), context, cancellationToken);
        }
    }

    public async Task DeleteAsync(
        List<ProvisionInput> inputs,
        ProvisionContext context,
        CancellationToken cancellationToken = default)
    {
        foreach (var group in inputs.GroupBy(x => x.Template.Provider))
        {
            await GetProvisioner(group.Key).DeleteAsync(group.ToList(), context, cancellationToken);
        }
    }

    private IProvisioner GetProvisioner(ResourceTemplateProvider provider)
    {
        if (!_provisioners.TryGetValue(provider, out var provisioner))
        {
            throw new InvalidOperationException(
                $"No provisioner for provider {provider}");
        }

        return provisioner;
    }
}
