using Orchitect.Infrastructure.Engine.Shared.CommandLine;

namespace Orchitect.Infrastructure.Engine.Provisioner.Terraform.Models;

public sealed record TerraformPlanResult
{
    public string WorkingDirectory { get; init; }
    public string PlanFilePath { get; init; }
    public string Message { get; init; }
    public int? ExitCode { get; init; }
    public TerraformPlanResultState State { get; init; }

    public TerraformPlanResult(TerraformPlanResultState state, string message)
    {
        State = state;
        Message = message;
        WorkingDirectory = string.Empty;
        PlanFilePath = string.Empty;
        ExitCode = null;
    }

    public TerraformPlanResult(string workingDirectory, string planFilePath, TerraformPlanResultState state,
        CommandLineResult? planCommandLineResult = null)
    {
        WorkingDirectory = workingDirectory;
        PlanFilePath = planFilePath;
        Message = planCommandLineResult switch
        {
            null => string.Empty,
            { ExitCode: not 0 and not (int)TerraformPlanResultExitCode.ChangesNeeded, StdErr.Length: > 0 } =>
                planCommandLineResult.StdErr,
            _ => planCommandLineResult.StdOut
        };
        State = state;
        ExitCode = planCommandLineResult?.ExitCode;
    }
}