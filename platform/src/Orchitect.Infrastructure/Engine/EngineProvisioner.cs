using Orchitect.Domain.Engine.ResourceTemplate;
using Orchitect.Infrastructure.Engine.Shared;

namespace Orchitect.Infrastructure.Engine;

public sealed record ProvisionInput(ResourceTemplate Template, Dictionary<string, string> Inputs, string Key);

public interface IEngineProvisioner
{
    Task ProvisionAsync(
        List<ProvisionInput> inputs,
        string folderName);
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
        string folderName)
    {
        var groups = inputs.GroupBy(x => x.Template.Provider);

        foreach (var group in groups)
        {
            if (!_provisioners.TryGetValue(group.Key, out var provisioner))
            {
                throw new InvalidOperationException(
                    $"No provisioner for provider {group.Key}");
            }

            await provisioner.ProvisionAsync(
                group.ToList(),
                folderName);
        }
    }
}