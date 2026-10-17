namespace Orchitect.Api.Integration.Tests.Helpers;

[CollectionDefinition("Integration")]
public sealed class IntegrationTestCollection : ICollectionFixture<WebApplicationFactoryWithPostgres>;
