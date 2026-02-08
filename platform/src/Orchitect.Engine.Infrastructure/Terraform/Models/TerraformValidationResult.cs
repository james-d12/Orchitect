namespace Orchitect.Engine.Infrastructure.Terraform.Models;

/// <summary>
/// A union essentially of either a Valid or Invalid Result.
/// Contains helper factory methods to reduce boilerplate when used.
/// </summary>
public abstract record TerraformValidationResult
{
    public ValidationResultState State { get; }
    public string Message { get; }

    public enum ValidationResultState
    {
        Valid,
        TemplateInvalid,
        ModuleInvalid,
        InputInvalid
    }

    private TerraformValidationResult(ValidationResultState state, string message)
    {
        State = state;
        Message = message;
    }

    public sealed record ValidResult(TerraformConfig Config, string ModuleDirectory, string Message)
        : TerraformValidationResult(ValidationResultState.Valid, Message);

    public sealed record InvalidResult(ValidationResultState State, string Message)
        : TerraformValidationResult(State, Message);

    public static ValidResult Valid(TerraformConfig config, string moduleDirectory) =>
        new(config, moduleDirectory, string.Empty);

    public static InvalidResult TemplateInvalid(string message) =>
        new InvalidResult(ValidationResultState.TemplateInvalid, message);

    public static InvalidResult ModuleInvalid(string message) =>
        new InvalidResult(ValidationResultState.ModuleInvalid, message);

    public static InvalidResult InputInvalid(string message) =>
        new InvalidResult(ValidationResultState.InputInvalid, message);
}