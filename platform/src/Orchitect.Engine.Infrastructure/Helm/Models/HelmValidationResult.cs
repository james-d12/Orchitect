namespace Orchitect.Engine.Infrastructure.Helm.Models;

public record HelmValidationResult
{
    public string Message { get; private init; } = string.Empty;
    public required HelmValidationResultState State { get; init; }
    public required List<HelmInput>? Config { get; init; }

    public static HelmValidationResult Valid(List<HelmInput> config) => new()
    {
        State = HelmValidationResultState.Valid,
        Config = config
    };

    public static HelmValidationResult WrongProvider(string message) => new()
    {
        Message = message,
        State = HelmValidationResultState.WrongProvider,
        Config = null
    };

    public static HelmValidationResult TemplateNotFound(string message) => new()
    {
        Message = message,
        State = HelmValidationResultState.TemplateNotFound,
        Config = null
    };

    public static HelmValidationResult ModuleNotFound(string message) => new()
    {
        Message = message,
        State = HelmValidationResultState.ModuleNotFound,
        Config = null
    };

    public static HelmValidationResult ModuleNotParsable(string message) => new()
    {
        Message = message,
        State = HelmValidationResultState.ModuleNotParsable,
        Config = null
    };

    public static HelmValidationResult InputNotPresent(string message) => new()
    {
        Message = message,
        State = HelmValidationResultState.InputNotPresent,
        Config = null
    };
}