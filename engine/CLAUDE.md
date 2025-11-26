# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

Conductor Engine is a .NET 10 (C# latest) web API for infrastructure orchestration that integrates with existing Infrastructure as Code (IaC) solutions. The project uses minimal APIs with a custom endpoint pattern, Entity Framework Core with SQLite, and JWT authentication.

## Building and Running

### Prerequisites
- .NET SDK 10.0.1 or later (specified in global.json)
- dotnet-ef tool version 10.0.0 (install via `scripts/setup.sh`)

### Development Commands

```bash
# Build the solution
dotnet build

# Run the API (from src/Conductor.Engine.Api)
dotnet run --project src/Conductor.Engine.Api

# Run the playground project (for testing/experimentation)
dotnet run --project src/Conductor.Engine.Playground

# Restore dependencies
dotnet restore
```

### Database Migrations

```bash
# Create a new migration (use the helper script)
./scripts/efm.sh <migration_name>

# Or manually from the Persistence project
cd src/Conductor.Engine.Persistence
dotnet ef migrations add <migration_name>
```

Note: The database is automatically migrated on application startup via `ApplyMigrations()` in Program.cs:85. The database is SQLite stored in the system temp directory.

### Testing

The project uses Stryker.NET for mutation testing (see stryker-config.json). Bruno API tests are available in `docs/Conductor Api - Bruno.json`.

## Architecture

This solution follows Clean Architecture principles with four main projects:

### 1. Conductor.Engine.Domain
**Purpose**: Core business logic and domain models - the "heart of the project"

**Key Concepts**:
- Contains all domain entities (Application, Environment, Deployment, Organisation, ResourceTemplate, Resource, etc.)
- Domain logic and entity relationships live here
- No dependencies on other layers

### 2. Conductor.Engine.Persistence
**Purpose**: Data access layer using Entity Framework Core

**Key Concepts**:
- `ConductorDbContext`: Main DbContext with all entity DbSets
- Database: SQLite stored in temp directory (`Path.GetTempPath()/Conductor.db`)
- Repository pattern for each aggregate root (IApplicationRepository, IEnvironmentRepository, etc.)
- Entity configurations in `Configurations/` directory using EF Fluent API
- Migrations in `Migrations/` directory
- **Important**: `ApplyMigrations()` calls `EnsureDeletedAsync()` before migrating (development-only behavior)

**Extension Pattern**:
- Uses C# extension syntax for service registration
- `AddPersistenceServices()` registers DbContext and repositories

### 3. Conductor.Engine.Infrastructure
**Purpose**: Integration with third-party IaC tools (Terraform, Helm)

**Key Concepts**:
- Contains "drivers" that apply resource templates to actual infrastructure
- Designed to integrate with existing IaC solutions rather than replacing them
- Subdirectories: CommandLine, Helm, Terraform, Score, Resources
- Uses extension pattern: `AddInfrastructureServices()`

### 4. Conductor.Engine.Api
**Purpose**: ASP.NET Core Web API with minimal API endpoints

**Key Concepts**:
- Uses **minimal APIs** with a custom endpoint pattern (NOT controllers)
- Endpoint pattern: Each endpoint implements `IEndpoint` interface with a static `Map()` method
- Endpoints organized by domain in `Endpoints/<Domain>/` directories
- Authentication: JWT Bearer tokens (configured in appsettings.json JwtOptions section)
- Authorization: Endpoints grouped into "Private" (requires auth) or "Public" (anonymous)
- Background task processing: `QueuedHostedService` with `IBackgroundTaskQueueProcessor` (capacity: 5)
- Identity: Uses ASP.NET Core Identity with custom table names (e.g., "Users" instead of "AspNetUsers")

**Endpoint Pattern Example**:
```csharp
public sealed class CreateOrganisationEndpoint : IEndpoint
{
    public static void Map(IEndpointRouteBuilder builder) => builder
        .MapPost("/", HandleAsync)
        .WithSummary("Creates a new organisation.");

    private static async Task<Results<Ok<Response>, InternalServerError>> HandleAsync(
        [FromBody] Request request,
        [FromServices] IRepository repository,
        CancellationToken cancellationToken)
    {
        // Implementation
    }
}
```

**Endpoint Organization**:
- Endpoints registered via extension methods in `Endpoints.cs`
- Groups created with tags: `/applications`, `/environments`, `/deployments`, `/resource-templates`, `/users`, `/organisations`
- Use `MapPrivateGroup()` for authenticated endpoints, `MapPublicGroup()` for anonymous

## Project Configuration

### Directory.Build.props
Strict compiler settings applied to all projects:
- .NET 10 (net10.0)
- Nullable reference types enabled
- TreatWarningsAsErrors: true
- Code analysis enforced during build
- Roslyn analyzers enabled

### Authentication & Authorization
- JWT Bearer authentication configured in Program.cs
- JwtOptions bound from configuration (Issuer, Audience, Secret)
- User registration and login endpoints in `/users` (public)
- All other endpoints require authentication

### API Documentation
- OpenAPI/Swagger enabled on all environments
- Swagger UI available at default endpoint
- JWT Bearer auth configured in Swagger (use "Bearer {token}" format)

## Development Patterns

### Adding a New Endpoint
1. Create endpoint class implementing `IEndpoint` in `src/Conductor.Engine.Api/Endpoints/<Domain>/`
2. Implement static `Map()` method to configure route
3. Implement static `HandleAsync()` method with typed results
4. Register in `Endpoints.cs` using `.MapEndpoint<YourEndpoint>()`

### Adding a New Domain Entity
1. Create entity in `src/Conductor.Engine.Domain/<Domain>/`
2. Add DbSet to `ConductorDbContext`
3. Create EF configuration in `src/Conductor.Engine.Persistence/Configurations/`
4. Create repository interface and implementation in `src/Conductor.Engine.Persistence/Repositories/`
5. Register repository in `PersistenceExtensions.AddPersistenceServices()`
6. Create migration using `./scripts/efm.sh <name>`

### Extension Methods Pattern
The codebase uses C# extension syntax for organizing extension methods:
```csharp
extension(IServiceCollection services)
{
    public IServiceCollection AddServices()
    {
        // Add services
        return services;
    }
}
```

## Important Notes

- **C# Language Version**: "latest" with "strict" features enabled
- **Database Behavior**: On startup, the database is deleted and recreated (see PersistenceExtensions.cs:32)
- **Authentication Required**: Most endpoints require JWT authentication (only `/users/register` and `/users/login` are public)
- **Background Tasks**: The API includes a background task queue processor with 5 worker capacity
- **User Secrets**: Configured with UserSecretsId in Api project for development credentials