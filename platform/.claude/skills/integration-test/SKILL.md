---
name: integration-test
description: Scaffold integration tests for new API endpoints in Orchitect.Api.Integration.Tests. Use when asked to write or generate integration tests for a new endpoint or entity.
user-invocable: true
allowed-tools:
  - Read
  - Edit
  - Write
  - Glob
  - Grep
  - Bash(dotnet test *)
---

# /integration-test — Scaffold Integration Tests

Generates integration test classes for new Orchitect API endpoints, following the established patterns in `platform/src/Orchitect.Api.Integration.Tests/`.

Arguments passed: `$ARGUMENTS`

---

## Step 1 — Understand what to test

If `$ARGUMENTS` is empty, ask the user which entity/endpoint to test before proceeding.

Otherwise, treat `$ARGUMENTS` as the entity name (e.g. `Issue`, `CloudSecret`, `Environment`). Then:

1. Search for the domain entity: `Orchitect.Domain.{Context}.{Domain}/{Entity}.cs`
2. Search for the endpoint classes: `Orchitect.Api/Endpoints/**/{Entity}*Endpoint.cs` or `*Controller.cs`
3. Identify the **context** (Core, Engine, or Inventory) from the namespace.

---

## Step 2 — Determine entity type

There are two distinct patterns depending on the context:

### Engine / Core entities (CRUD via HTTP)
- Created, updated, deleted through the API directly — no seed helpers.
- Use `PostAsJsonAsync`, `PutAsJsonAsync`, `DeleteAsync`, `GetAsync`.
- Example: `EnvironmentIntegrationTests`, `ApplicationIntegrationTests`.

### Inventory entities (read-only, seeded directly into DB)
- Discovered externally; the API only reads them. Seed via `InventorySeedHelper`.
- Use `factory.Seed{Entity}Async(organisationId)` to create test data.
- Always test organisation isolation (data from other orgs is not returned).
- Example: `CloudResourceIntegrationTests`, `RepositoryIntegrationTests`, `PullRequestIntegrationTests`.

---

## Step 3 — Check for existing seed helper (Inventory only)

Read `platform/src/Orchitect.Api.Integration.Tests/Helpers/InventorySeedHelper.cs`.

If `Seed{Entity}Async` does not already exist:
1. Read the domain entity class to discover its properties and ID type.
2. Read the relevant repository interface (`I{Entity}Repository`) to determine the upsert method (`UpsertAsync` or `BulkUpsertAsync`).
3. Add a new `Seed{Entity}Async` extension method on `WebApplicationFactoryWithPostgres` following this template:

```csharp
public static async Task<{Entity}> Seed{Entity}Async(
    this WebApplicationFactoryWithPostgres factory,
    OrganisationId organisationId,
    {EntityPlatform}? platform = null)
{
    var entity = new {Entity}
    {
        Id = new {Entity}Id(Fixture.Create<string>()),
        OrganisationId = organisationId,
        // ... populate all required properties with Fixture.Create<T>()
        // use new Uri($"https://example.com/{Fixture.Create<string>()}") for URLs
        // use DateTime.UtcNow for DiscoveredAt and UpdatedAt
        Platform = platform ?? Fixture.Create<{EntityPlatform}>(),
    };

    using var scope = factory.Services.CreateScope();
    var repository = scope.ServiceProvider.GetRequiredService<I{Entity}Repository>();
    await repository.BulkUpsertAsync([entity]);

    return entity;
}
```

Add required `using` directives at the top of the file (domain namespace, services namespace).

---

## Step 4 — Determine which tests to write

### For GET by ID endpoints, always write:
- `{Entity}Api_WhenGetting{Entity}ById_ShouldReturn200Ok` — seed, GET by id, assert all response fields match seeded values
- `{Entity}Api_WhenGetting{Entity}ByNonExistentId_ShouldReturn404NotFound` — GET with `_fixture.Create<string>()` as id

### For GET all / list endpoints, always write:
- `{Entity}Api_WhenGettingAll{Entities}_ShouldReturn200Ok` — seed one, GET collection with `?organisationId=`, assert non-empty
- `{Entity}Api_WhenGettingAll{Entities}_ShouldNotReturnResourcesFromOtherOrganisations` — seed in two orgs, query one, assert isolation

For each **query filter** the endpoint accepts (name, platform, url, status, etc.), write:
- `{Entity}Api_WhenGettingAll{Entities}_ShouldFilterBy{FilterName}` — seed two with different values, query by one, assert `Single` result

### For POST (create) endpoints (Engine/Core), write:
- `{Entity}Api_WhenCreating{Entity}_ShouldReturn200Ok` — POST valid request, assert status and returned id is a Guid

### For PUT (update) endpoints (Engine/Core), write:
- `{Entity}Api_WhenUpdating{Entity}_ShouldReturn200Ok` — create then update, assert updated fields
- `{Entity}Api_WhenUpdatingNonExistent{Entity}_ShouldReturn404NotFound`

### For DELETE endpoints (Engine/Core), write:
- `{Entity}Api_WhenDeleting{Entity}_ShouldReturn204NoContent` — create then delete, assert 204
- `{Entity}Api_WhenDeletingNonExistent{Entity}_ShouldReturn404NotFound`
- `{Entity}Api_WhenGetting{Entity}AfterDeletion_ShouldReturn404NotFound` — create, delete, then GET same id

---

## Step 5 — Write the test file

Create `platform/src/Orchitect.Api.Integration.Tests/{Entity}IntegrationTests.cs` using this structure:

```csharp
using System.Net;
using AutoFixture;
using Orchitect.Api.Endpoints.{Context}.{Domain};
using Orchitect.Api.Integration.Tests.Helpers;
using Orchitect.Domain.Core.Organisation;
using Orchitect.Domain.{Context}.{Domain};

namespace Orchitect.Api.Integration.Tests;

[Collection("Integration")]
public sealed class {Entity}IntegrationTests(WebApplicationFactoryWithPostgres factory)
{
    private const string {Entities}Url = "/{route}";
    private readonly Fixture _fixture = new();

    // For Engine/Core only — builder methods for request objects:
    // private {CreateRequest} BuildCreateRequest(Guid organisationId) => ...
    // private {UpdateRequest} BuildUpdateRequest() => ...

    [Fact]
    public async Task {Entity}Api_When..._Should...()
    {
        // Arrange
        var client = await factory.CreateClient().AddAuthorisationHeader();
        var organisation = await client.CreateOrganisationAsync();
        // seed or create entity...

        // Act
        var response = await client.GetAsync(...);
        var body = await response.ReadFromJsonAsync<{EndpointClass}.{ResponseType}>();

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        // field assertions...
    }
}
```

**Key rules:**
- Class is `sealed`, constructor-injected with `WebApplicationFactoryWithPostgres factory`
- `[Collection("Integration")]` on the class
- `_fixture` is `new Fixture()` (AutoFixture) — use for random string/enum values and non-existent IDs
- Always `await factory.CreateClient().AddAuthorisationHeader()` — all endpoints require auth
- Always call `CreateOrganisationAsync()` when the entity requires an `OrganisationId`
- Response types come from the static nested classes on endpoint classes (e.g. `GetCloudResourceEndpoint.GetCloudResourceResponse`)
- Use `response.ReadFromJsonAsync<T>()` (the `HttpMessageExtensions` helper)
- Test method names follow pattern: `{Entity}Api_When{Condition}_Should{ExpectedOutcome}`

---

## Step 6 — Verify

Run the tests to confirm they pass:

```bash
cd platform && dotnet test src/Orchitect.Api.Integration.Tests --filter "{Entity}IntegrationTests"
```

Fix any compilation errors before reporting done. If tests fail due to missing seed data, missing using directives, or wrong response type names — investigate and fix.