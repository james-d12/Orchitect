namespace Orchitect.Engine.Contracts.Runner.Api;

public sealed record RunInput(
    string Key,
    string TemplateName,
    string TemplateType,
    RunInputProvider Provider,
    RunInputSource Source,
    IReadOnlyDictionary<string, string> Parameters,
    IReadOnlyList<string>? PreviousKeys = null);
