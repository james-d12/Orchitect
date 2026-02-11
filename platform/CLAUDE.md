# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

Orchitect is a modular internal developer platform (IDP) built as a .NET 10 (C# latest) solution using .NET Aspire for orchestration. The platform is composed of a shared **Core** foundation and independent **capabilities** (Engine, Inventory), each with its own domain, persistence, and API layer. All services share a single PostgreSQL database with schema isolation.

## Building and Running

```bash
# Build the solution
dotnet build

# Run all services via Aspire AppHost (preferred)
dotnet run --project src/Orchitect.AppHost

# Run individual APIs
dotnet run --project src/Orchitect.Core.Api
dotnet run --project src/Orchitect.Engine.Api
dotnet run --project src/Orchitect.Inventory.Api

# Run the playground (manual start in Aspire, or standalone)
dotnet run --project src/Orchitect.Playground
```

### Prerequisites

- .NET SDK 10.0.2+ (specified in global.json with latestMinor rollForward)
- dotnet-ef tool (install via `scripts/setup.sh`)
- PostgreSQL (provided automatically by Aspire AppHost on port 41031)

### Database Migrations

Each bounded context has its own EF Core migrations. The helper script runs migrations across all three:

```bash
# Create migration across all persistence projects
./scripts/efm.sh <migration_name>

# Or individually from a specific persistence project
cd src/Orchitect.Core.Persistence && dotnet ef migrations add <name>
cd src/Orchitect.Engine.Persistence && dotnet ef migrations add <name>
cd src/Orchitect.Inventory.Persistence && dotnet ef migrations add <name>
```

All migrations are applied automatically on application startup. **The database is deleted and recreated on each startup** (development-only behavior).

### Testing

```bash
# Run unit tests (xUnit + AutoFixture)
dotnet test

# Run a single test project
dotnet test src/Orchitect.Inventory.Infrastructure.Tests
```

Stryker.NET is configured for mutation testing (see stryker-config.json). Bruno API tests are available in `docs/Orchitect Api - Bruno.json`.

## Architecture

The platform follows a hub-and-spoke model: **Core** at the center, **capabilities** radiate outward. See `docs/HIGH_LEVEL_ARCHITECTURE.md` for the full design rationale.

```
          Inventory
              |
Analysis ─── Core ─── Engine
              |
        (future capabilities)
```

**Hard dependency rules:**
- Capabilities (Engine, Inventory) → Core: allowed
- Core → any capability: forbidden
- Capability → capability: forbidden (coordinate via Core or events)

### Aspire Orchestration (AppHost)

`src/Orchitect.AppHost/Program.cs` defines the service topology:
- **PostgreSQL** on port 41031, database "orchitect"
- **Core API** starts first, waits for database
- **Engine API** and **Inventory API** wait for Core API
- **Portal Web** (JavaScript/pnpm at `../../../portals/Orchitect.Portal.Web`, port 3001) waits for all APIs

### Bounded Contexts

Each bounded context follows Clean Architecture: Domain → Persistence → Api.

**Core** (`Orchitect.Core.*`) — Schema: `core`
- Owns identity (ASP.NET Identity), organisations, teams
- Organisation is the tenant boundary shared by all capabilities
- Provides user registration/login (public) and organisation CRUD (authenticated)
- Uses minimal APIs with IEndpoint pattern

**Engine** (`Orchitect.Engine.*`) — Schema: `engine`
- Infrastructure orchestration: applications, environments, deployments, resource templates, services
- All entities reference OrganisationId from Core via cross-schema foreign keys with cascade delete
- Background task queue (QueuedHostedService, capacity 5) for async processing
- Infrastructure layer integrates with Terraform, Helm, and other IaC tools
- Uses minimal APIs with IEndpoint pattern

**Inventory** (`Orchitect.Inventory.*`) — Schema: `inventory`
- Discovery and cataloging of external resources: cloud resources, git repos, pipelines, work items
- Integrates with Azure, Azure DevOps, GitHub, GitLab
- DiscoveryHostedService for periodic automated discovery
- **Uses MVC controllers** (not minimal APIs, unlike Core/Engine)

### Shared Projects

- **Orchitect.Shared**: Common abstractions (`IEndpoint`, `ErrorResponse`) referenced by all API projects
- **Orchitect.ServiceDefaults**: Aspire shared project providing OpenTelemetry, service discovery, resilience handlers, and health check endpoints (`/health`, `/alive`)

### Database Architecture

Single PostgreSQL database with schema-per-context:
- Connection string via Aspire: `ConnectionStrings__orchitect`
- Cross-schema foreign keys enforce referential integrity (e.g., engine.Applications → core.Organisations with cascade delete)
- Each context has its own DbContext and migrations
- Startup order matters: Core migrations run first (Aspire ensures Core API starts before Engine/Inventory)

### Key Patterns

**Strongly-typed IDs**: All entities use record-wrapped Guids (e.g., `OrganisationId(Guid Value)`). EF Core value conversions handle persistence.

**Endpoint pattern** (Core & Engine):
```csharp
public sealed class CreateOrganisationEndpoint : IEndpoint
{
    public static void Map(IEndpointRouteBuilder builder) => builder
        .MapPost("/", HandleAsync)
        .WithSummary("Creates a new organisation.");

    private static async Task<Results<Ok<Response>, InternalServerError>> HandleAsync(
        [FromBody] Request request,
        [FromServices] IRepository repository,
        CancellationToken cancellationToken) { /* ... */ }
}
```

**Extension methods**: Service registration uses C# extension syntax:
```csharp
extension(IServiceCollection services)
{
    public IServiceCollection AddCorePersistenceServices() { /* ... */ }
}
```

**Repository pattern**: Core and Engine use `IRepository<T, TId>` with concrete implementations. Inventory uses direct DbContext access (query-focused).

**Endpoint groups**: `MapPrivateGroup()` for authenticated endpoints, `MapPublicGroup()` for anonymous.

## Development Patterns

### Adding a New Endpoint (Core/Engine)
1. Create class implementing `IEndpoint` in `src/Orchitect.{Context}.Api/Endpoints/{Domain}/`
2. Implement static `Map()` and `HandleAsync()` methods
3. Register in the context's `Endpoints.cs` via `.MapEndpoint<YourEndpoint>()`

### Adding a New Domain Entity
1. Create entity in `src/Orchitect.{Context}.Domain/{Domain}/`
2. Add DbSet to the context's DbContext
3. Create EF configuration in `src/Orchitect.{Context}.Persistence/Configurations/`
4. Create repository interface and implementation
5. Register repository in the context's `Add{Context}PersistenceServices()` extension
6. Create migration: `cd src/Orchitect.{Context}.Persistence && dotnet ef migrations add <name>`

## Project Configuration

- **Directory.Build.props**: TreatWarningsAsErrors, nullable enabled, Roslyn analyzers enforced, NuGet audit on all transitive dependencies
- **Authentication**: JWT Bearer tokens (JwtOptions from appsettings.json). Only `/users/register` and `/users/login` are public; all other endpoints require auth
- **API docs**: OpenAPI/Swagger on all environments with JWT Bearer security definition
