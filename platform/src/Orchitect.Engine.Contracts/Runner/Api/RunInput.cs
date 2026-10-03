namespace Orchitect.Engine.Contracts.Runner.Api;

public sealed record RunInput(
    string Key,
    string TemplateType,
    RunInputSource Source,
    IReadOnlyDictionary<string, string> Parameters);
