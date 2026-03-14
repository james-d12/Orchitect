namespace Orchitect.Domain.Engine.Application;

public sealed record CreateRepositoryRequest(string Name, Uri Url, RepositoryProvider Provider);