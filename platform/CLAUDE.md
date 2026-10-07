# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

Orchitect is a modular internal developer platform (IDP) built as a .NET 10 (C# latest) solution using .NET Aspire for orchestration. The code is organised around a shared **Core** (identity, organisations, credentials) and **capabilities** (Engine, Inventory). They are served by one API and share one PostgreSQL database. Provisioning runs out of process in a disposable **runner** container.

## Building and Running

```bash
# Build the solution
dotnet build

# Run everything via the Aspire AppHost (preferred)
dotnet run --project src/Orchitect.AppHost

# Run the API on its own
dotnet run --project src/Orchitect.Api

# Build the runner image that deployments run in (from platform/, BuildKit required)
src/Orchitect.Runner/docker-build.sh terraform

# Run the playground (manual start in Aspire, or standalone)
dotnet run --project src/Orchitect.Playground
```

### Prerequisites

- .NET SDK 10.0.2+ (specified in global.json with latestMinor rollForward)
- Local dotnet tools: dotnet-ef, dotnet-stryker, dotnet-sonarscanner (install via `dotnet tool restore`)
- Docker, for the runner containers and the integration tests (Testcontainers)
- PostgreSQL (provided automatically by Aspire AppHost on port 41031)

### Database Migrations

There is one `OrchitectDbContext` in `Orchitect.Persistence`, with one set of migrations:

```bash
./scripts/efm.sh <migration_name>

# or
cd src/Orchitect.Persistence && dotnet ef migrations add <name>
```

The API applies pending migrations on startup (`ApplyMigrations()` in `Orchitect.Api/Program.cs`).

### Testing

```bash
# Run all tests
dotnet test

# Run a single test project
dotnet test src/Orchitect.Engine.Execution.Unit.Tests
```

Test projects:
- `Orchitect.AppHost.E2E.Tests`: boots the real Aspire AppHost and checks every resource becomes healthy (needs Docker, Node.js and pnpm)
- `Orchitect.Api.Integration.Tests`: endpoints and repositories against Postgres in Testcontainers (needs Docker)
- `Orchitect.Domain.Unit.Tests`: domain entity behaviour (e.g. deployment status transitions)
- `Orchitect.Engine.Dispatch.Unit.Tests`: executor, queue, Key Vault and run token minting, the run planner, completer and completion handler, plus the API-to-runner contract round-trip, the runner's command line without configuration and the Engine layering guard tests
- `Orchitect.Engine.Execution.Unit.Tests`: orchestrator, drivers, runner secret loading and the runner API client
- `Orchitect.Infrastructure.Inventory.Unit.Tests`, `Orchitect.Common.Unit.Tests`

Stryker.NET is configured for mutation testing (see stryker-config.json). Bruno API tests are in `bruno/Orchitect API Collection` (the `E2E` folder runs a full deployment flow).

## Architecture

The platform follows a hub-and-spoke model: **Core** at the center, **capabilities** radiate outward. See `docs/architecture/HIGH_LEVEL_ARCHITECTURE.md` for the full design rationale.

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

Each capability is a namespace folder (`Core`, `Engine`, `Inventory`) inside the shared projects below, not a separate project.

### Projects

| Project | Role |
|---|---|
| `Orchitect.Api` | The single ASP.NET API. Minimal-API endpoints in `Endpoints/{Core,Engine,Inventory}/`, plus `Jobs/DiscoveryHostedService` for periodic Inventory discovery. Every discovery, scheduled or triggered, goes through `Jobs/DiscoveryRunner`, which records a `DiscoveryRun` (status, counts, error) readable at `GET /discovery/{id}/status` and `/runs` |
| `Orchitect.Domain` | Entities, strongly-typed IDs and repository interfaces, in `Core/`, `Engine/` and `Inventory/` |
| `Orchitect.Persistence` | `OrchitectDbContext`, EF configurations, repositories and migrations for all contexts |
| `Orchitect.Engine.Contracts` | What the API and the runner must agree on: runner arguments and environment keys, `RunnerOperation`, `TerraformBackendOptions`, `SecretProviderOptions`, the secrets file, the Score models, and the runner API routes, DTOs and contract version (`Runner/Api/`). No Orchitect references |
| `Orchitect.Engine.Dispatch` | Control plane, used by the API: `IExecutor`/`DockerExecutor`, deployment queue, runner container sweep, Key Vault token minting, `IRunPlanner` (template resolution and resource recording for a run), `IRunCompleter` (moving a run's instances to their final status) and `IRunCompletionHandler` (finishing a run). The runner must not reference it (`EngineLayeringTests`) |
| `Orchitect.Engine.Execution` | Data plane, used by the runner and the Playground: `EngineOrchestrator`, Score, Terraform and Helm drivers, secret providers, the runner API client (`IRunnerApiClient`) |
| `Orchitect.Infrastructure.Inventory` | Discovery integrations for Azure, Azure DevOps, GitHub and GitLab, one folder per provider plus `Shared` |
| `Orchitect.Runner` | Console app packaged as the runner image. Takes `--run-id`, runs that provision or destroy through the runner API, then exits. No database access: it references only Execution and ServiceDefaults |
| `Orchitect.Common` | Shared helpers (observability, query, extensions) |
| `Orchitect.ServiceDefaults` | Aspire defaults: OpenTelemetry, service discovery, resilience, `/health` and `/alive` |
| `Orchitect.Playground` | Runs Engine provisioning in-process for experiments |
| `Orchitect.AppHost` | Aspire orchestration |

### Aspire Orchestration (AppHost)

`src/Orchitect.AppHost/Program.cs` defines the topology:
- **PostgreSQL** on port 41031, database "orchitect"
- **orchitect-api** on port 41005 (Swagger at `/swagger`), waits for the database. `ExecutorOptions__*` settings configure the runner containers, including the Key Vault secret mappings (`keyvault-uri` parameter)
- **orchitect-playground**, manual start
- **Portal Web** (pnpm app at `../../../portals/Orchitect.Portal.Web`, port 3001), waits for the API

### Deployments and the runner

`POST /deployments` stores a `Pending` deployment with a `Queued` `DeploymentRun` and queues it in the in-memory `DeploymentQueue` (capacity 5, processed one at a time). The work item starts an `orchitect-runner-{runId}` container through `DockerExecutor`; the run ID is also its `orchitect.run-id` label, its only argument (`--run-id`) and `ORCHITECT_RUN_ID`. The runner reaches the API at `ExecutorOptions:ApiBaseUrl` (`ORCHITECT_API_URL`) with the run token from `secrets.json`, and never touches the database. It fetches the run descriptor (`GET /internal/runs/{runId}`), clones the application repository at the commit, parses the score file and submits it for a plan: the API-side `IRunPlanner` resolves the resource templates, records the resources and instances, and stores the plan for the run in one transaction (`POST /internal/runs/{runId}/plan`, `CreateRunPlanEndpoint`). A parameter can use another resource's output as `${resources.<key>.<output>}`: the planner records the dependency and rejects references to unknown keys, to itself, across providers or in a cycle, and the runner checks the output exists and renders the reference as a `module.<name>.<output>` expression, so Terraform resolves it in dependency order. The runner then loads its mapped secrets, runs Terraform and reports the outcome (`POST /internal/runs/{runId}/complete`). Every run that isn't cancelled ends through `IRunCompletionHandler`: the runner's report, the `DeploymentQueue` exit-code fallback when the runner exits without reporting, or the sweep. Under the run's row lock, it finishes the run, sets the deployment to `Deployed`, `Destroyed` or `Failed` and revokes the run token, then calls `IRunCompleter` to move the instances to their final status. Once a report has completed the run, the exit code is only recorded. `DELETE /deployments/{id}` destroys the latest deployment the same way, with a new `Destroy` run. `GET /deployments/{id}` returns the latest run. `POST /deployments/{id}/cancel` cancels the latest run. A queued run is cancelled straight away and skipped when it is dequeued. A running one gets `CancelRequestedAt` and is stopped: through `IDeploymentRunCancellation` when this process owns it (`DockerExecutor` sends SIGTERM and waits `StopGracePeriod` before force-removing the container), or with `IExecutor.SignalStopAsync` when an earlier process started it (the sweep records it once the runner exits). The planner refuses a run with `CancelRequestedAt`, and when the API records a cancel it also calls `IRunCompleter` with `Failed` so planned instances don't stay `Provisioning`/`Removing`. The run ends `Cancelled` unless the runner exited 0 first. `DeploymentRun.Version` maps to Postgres `xmin`, so `IDeploymentRunRepository.UpdateAsync` throws `DeploymentRunConflictException` for a stale run; whoever moves a run out of `Queued` first wins. A cancelled deployment can be destroyed. Only one deployment per application/environment can be active at a time (`IX_Deployments_ActiveRun`). `RunnerContainerSweepService` removes leftover containers and reconciles deployments left active by an earlier API process. See `docs/runner/` for the design and open work.

### Database Architecture

- One PostgreSQL database and one `OrchitectDbContext`. Inventory tables live in the `inventory` schema; Core and Engine tables use the default schema
- Connection string via Aspire: `ConnectionStrings__orchitect`
- Entities reference `OrganisationId` from Core through foreign keys with cascade delete

### Key Patterns

**Strongly-typed IDs**: All entities use record-wrapped Guids (e.g., `OrganisationId(Guid Value)`). EF Core value conversions handle persistence.

**Endpoint pattern**:
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

**Extension methods**: each project registers its services through a static `IServiceCollection` extension:
```csharp
public static IServiceCollection AddPersistenceServices(this IServiceCollection services) { /* ... */ }
```
`Orchitect.Engine.Execution` has `AddEngineProvisioningServices()` and `AddRunnerServices(configuration)` for the runner and the playground. `Orchitect.Engine.Dispatch` has `AddEngineDispatchServices(configuration)` for the API. Dispatch and Execution must not reference each other.

**Repository pattern**: repository interfaces live in `Orchitect.Domain` (Core and Engine extend `IRepository<T, TId>`), with implementations in `Orchitect.Persistence/Repositories/{Context}/`.

**Endpoint groups**: `MapPrivateGroup()` for authenticated endpoints, `MapPublicGroup()` for anonymous.

**Organisation membership**: every authorised endpoint declares how it is scoped in its `Map`. Use `.RequireOrganisationMember()` for a `Guid organisationId` parameter, `.RequireOrganisationMember<TRequest>(r => r.OrganisationId)` for an id in a request (both 403), `.RequireOrganisationAccess<TEntity, TId>(...)` for an entity loaded by `id` (404), or `.HandlesOrganisationScope()` when the handler filters to `IOrganisationAccess.GetOrganisationIdsAsync()` itself (see `Shared/Authorization`). `OrganisationScopeCoverageTests` fails for an endpoint that declares none. Creating an organisation makes the caller a member; members add and remove others under `/organisations/{id}/members`.

## Documentation

Design docs live in `docs/`, grouped by workstream (`runner/`, `resource/`, `architecture/`), with finished or superseded docs in `docs/archive/`. `docs/README.md` is the index. Outstanding work is tracked as GitHub issues on `james-d12/Orchitect` (one milestone per workstream), and each doc lists its issues in YAML front matter. Use the `/orchitect-docs` skill to read, create or update docs so the docs, the index and the issues stay consistent.

## Development Patterns

### Adding a New Endpoint
1. Create a class implementing `IEndpoint` in `src/Orchitect.Api/Endpoints/{Context}/{Domain}/`
2. Implement static `Map()` and `HandleAsync()` methods
3. Register it in `{Context}Endpoints.cs` via `.MapEndpoint<YourEndpoint>()`
4. Add integration tests in `Orchitect.Api.Integration.Tests` (the `/integration-test` skill scaffolds them)

### Adding a New Domain Entity
1. Create the entity in `src/Orchitect.Domain/{Context}/{Domain}/`
2. Add a DbSet to `OrchitectDbContext`
3. Create its EF configuration in `src/Orchitect.Persistence/Configurations/{Context}/`
4. Create the repository interface in `Orchitect.Domain` and its implementation in `src/Orchitect.Persistence/Repositories/{Context}/`
5. Register the repository in `AddPersistenceServices()`
6. Create a migration: `./scripts/efm.sh <name>`

## Project Configuration

- **Directory.Build.props**: TreatWarningsAsErrors, nullable enabled, Roslyn analyzers enforced, NuGet audit on all transitive dependencies
- **Authentication**: JWT Bearer tokens (JwtOptions from appsettings.json) for users, registered by `AddOrchitectAuthentication()`. Only `/users/register` and `/users/login` are public; all other endpoints require auth. The runner API under `/internal/runs/{runId}` (`MapRunnerGroup()`) accepts only the `Runner` scheme: a per-run opaque token whose SHA-256 hash is on `DeploymentRun.TokenHash`, valid while the run is Queued/Running and unexpired, and only for the run in the route. Every runner API request must also send `Orchitect-Runner-Contract: <RunnerContract.Version>` (`RunnerContractFilter`), or it gets a 400
- **API docs**: OpenAPI/Swagger on all environments with JWT Bearer security definition
