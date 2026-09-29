namespace Orchitect.Engine.Execution.Shared.CommandLine;

public sealed record CommandLineResult(string StdOut, string StdErr, int ExitCode);