namespace Orchitect.Infrastructure.Engine.Helm.Models;

public enum HelmValidationResultState
{
    WrongProvider,
    TemplateNotFound,
    ModuleNotFound,
    ModuleNotParsable,
    InputNotPresent,
    Valid
}