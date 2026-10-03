using Microsoft.Extensions.Options;

namespace Orchitect.Engine.Execution.RunnerApi;

public sealed class RunnerApiOptionsValidator : IValidateOptions<RunnerApiOptions>
{
    public ValidateOptionsResult Validate(string? name, RunnerApiOptions options) =>
        options.GetValidationError() is { } error
            ? ValidateOptionsResult.Fail(error)
            : ValidateOptionsResult.Success;
}
