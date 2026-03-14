namespace Orchitect.Infrastructure.Engine.CommandLine;

public sealed record CommandLineResult(string StdOut, string StdErr, int ExitCode);