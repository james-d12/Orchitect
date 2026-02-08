using Conductor.Engine.Infrastructure.CommandLine;

namespace Conductor.Engine.Infrastructure.Terraform.Models;

public sealed record TerraformPlanResult
{
    public string StateDirectory { get; init; }
    public string PlanFilePath { get; init; }
    public string Message { get; init; }
    public int? ExitCode { get; init; }
    public TerraformPlanResultState State { get; init; }

    public TerraformPlanResult(TerraformPlanResultState state, string message)
    {
        State = state;
        Message = message;
        StateDirectory = string.Empty;
        PlanFilePath = string.Empty;
        ExitCode = null;
    }

    public TerraformPlanResult(string stateDirectory, string planFilePath, TerraformPlanResultState state,
        CommandLineResult? planCommandLineResult = null)
    {
        StateDirectory = stateDirectory;
        PlanFilePath = planFilePath;
        Message = planCommandLineResult?.StdOut ?? planCommandLineResult?.StdErr ?? string.Empty;
        State = state;
        ExitCode = planCommandLineResult?.ExitCode;
    }
}