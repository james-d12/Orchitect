using Microsoft.Extensions.Logging;
using Orchitect.Engine.Infrastructure.Helm.Models;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Orchitect.Engine.Infrastructure.Helm;

public interface IHelmParser
{
    Task<List<HelmInput>> ParseHelmConfigAsync(string helmChartDirectory);
}

public sealed class HelmParser : IHelmParser
{
    private readonly ILogger<HelmParser> _logger;

    public HelmParser(ILogger<HelmParser> logger)
    {
        _logger = logger;
    }

    public async Task<List<HelmInput>> ParseHelmConfigAsync(string helmChartDirectory)
    {
        var valuesFile = Directory
            .GetFiles(helmChartDirectory, "values.yaml", SearchOption.AllDirectories)
            .FirstOrDefault();

        if (valuesFile is null)
        {
            _logger.LogWarning("Could not find values.yaml in template directory: {Directory}", helmChartDirectory);
            return [];
        }

        var fileContents = await File.ReadAllTextAsync(valuesFile);

        var yamlObject = new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .Build()
            .Deserialize<object>(fileContents);

        var inputs = new List<HelmInput>();
        Traverse(yamlObject, string.Empty, inputs);

        return inputs;
    }

    private static void Traverse(object? node, string prefix, IList<HelmInput> inputs)
    {
        switch (node)
        {
            case IDictionary<object, object> map:
                foreach (var kvp in map)
                {
                    var key = kvp.Key.ToString() ?? string.Empty;
                    var fullKey = string.IsNullOrEmpty(prefix) ? key : $"{prefix}.{key}";
                    Traverse(kvp.Value, fullKey, inputs);
                }

                break;

            case IList<object> list:
                for (var i = 0; i < list.Count; i++)
                {
                    var fullKey = $"{prefix}[{i}]";
                    Traverse(list[i], fullKey, inputs);
                }

                break;

            default:
                inputs.Add(new HelmInput(prefix, node));
                break;
        }
    }
}