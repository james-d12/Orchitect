namespace Conductor.Engine.Infrastructure.CommandLine;

public sealed record CommandLineResult(string StdOut, string StdErr, int ExitCode);