namespace Orchitect.Engine.Api.Common;

public sealed record ErrorResponse
{
    public required List<Error> Errors { get; init; }
}

public sealed record Error
{
    public required string Code { get; init; }
    public required string Message { get; init; }
}