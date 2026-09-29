namespace Orchitect.Engine.Execution.Provisioner.Helm.Models;

public enum HelmValidationResultState
{
    WrongProvider,
    TemplateNotFound,
    ModuleNotFound,
    ModuleNotParsable,
    InputNotPresent,
    Valid
}