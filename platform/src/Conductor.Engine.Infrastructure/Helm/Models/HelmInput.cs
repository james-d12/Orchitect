namespace Conductor.Engine.Infrastructure.Helm.Models;

public sealed record HelmInput(string Key, object? DefaultValue);