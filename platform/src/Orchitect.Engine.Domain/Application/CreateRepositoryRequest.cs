namespace Orchitect.Engine.Domain.Application;

public sealed record CreateRepositoryRequest(string Name, Uri Url, RepositoryProvider Provider);