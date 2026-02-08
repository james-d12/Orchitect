namespace Orchitect.Engine.Infrastructure.Helm.Models;

public enum HelmValidationResultState
{
    WrongProvider,
    TemplateNotFound,
    ModuleNotFound,
    ModuleNotParsable,
    InputNotPresent,
    Valid
}