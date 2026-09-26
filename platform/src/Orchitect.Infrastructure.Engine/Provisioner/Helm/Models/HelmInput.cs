namespace Orchitect.Infrastructure.Engine.Provisioner.Helm.Models;

public sealed record HelmInput(string Key, object? DefaultValue);