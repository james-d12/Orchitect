namespace Orchitect.Infrastructure.Engine.Shared.CommandLine;

public sealed record CommandLineResult(string StdOut, string StdErr, int ExitCode);