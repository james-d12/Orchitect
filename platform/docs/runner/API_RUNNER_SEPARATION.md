---
title: "API / runner separation (target design)"
status: active
workstream: runner
milestone: "Runner Isolation"
issues: [102, 103, 105, 106, 107, 110, 202, 203, 204, 213, 214, 215]
superseded_by: null
last_reviewed: 2026-10-03
---

# Orchitect Runner Architecture

## 1. Purpose

Refactor `Orchitect.Runner` into a lightweight, disposable execution worker.

The Runner should **not have direct access to the Orchitect database**. Instead, it communicates with the Orchitect API over an authenticated internal API.

The Orchitect API remains responsible for:

* Persistent state
* Run lifecycle
* Score configuration
* Resource/template resolution
* Environment configuration
* Runner coordination
* Recording execution results

The Runner is responsible for:

* Obtaining its execution context
* Executing the requested orchestration
* Invoking Terraform, Helm, cloud providers, etc.
* Reporting progress and results
* Returning a meaningful exit status

## Decision (#102)

The runner calls an internal API on Orchitect.API and never connects to the database. We considered and rejected two alternatives:

- **A read-only Postgres role.** The runner now writes `Resource`, `ResourceInstance` and `ResourceDependencyGraph` rows, so it can't run read-only. Even with only `SELECT`, untrusted Terraform could read every organisation's data (H1), and the runner image would still carry the EF model, so it would break when the API's schema changes (A1).
- **A manifest file mounted into the container.** The API can't build the full manifest up front, because the score file lives in the application repo and the API has no git. The runner also still has to report results back, so a callback channel is needed anyway.

The runner is **thin**: all domain and database logic lives in the API. The runner clones repositories, parses the score file, runs IaC tools and reports results.

---

# 2. Architecture

```text
                         ┌──────────────────────────┐
                         │      Orchitect API       │
                         │                          │
                         │  Application             │
                         │  Orchestration           │
                         │  Persistence             │
                         │  Resource Templates      │
                         │  Score Configuration     │
                         └────────────┬─────────────┘
                                      ▲
                       /internal/runs/{runId}/*
                       per-run bearer token
                                      │
                         ┌────────────┴─────────────┐
                         │    Orchitect Runner      │
                         │                          │
                         │  Run API client          │
                         │  Score parsing (git)     │
                         │  Secret loading          │
                         │  Provisioners            │
                         │  Terraform               │
                         │  Helm                    │
                         └──────────────────────────┘
                                      │
                                      ▼
                              Infrastructure
```

The Runner is a worker attached to exactly one run. Every request goes from the runner to the API. The API never calls into the container.

---

# 3. Run identity

The runner API needs a stable run ID, because one deployment can have several runs (provision, destroy, retries), and the token and every endpoint below are scoped to a single run.

`DeploymentRun` provides it (#110). Every provision and destroy creates one, and its ID is the container name, the `orchitect.run-id` label and `ORCHITECT_RUN_ID`:

| Field | Purpose |
|---|---|
| `Id` | Run identity: route parameter, container name and labels, `ORCHITECT_RUN_ID`, tracing |
| `DeploymentId` | Parent deployment |
| `Operation` | `Provision` / `Destroy` (`RunnerOperation`) |
| `Status` | `Queued` / `Running` / `Succeeded` / `Failed` / `Cancelled` |
| `QueuedAt`, `StartedAt`, `FinishedAt` | Lifecycle timestamps |
| `ExitCode` | Container exit code, recorded even when a report exists |
| `ErrorSummary` | From the runner's report, or derived from the exit code |
| `RunnerId` | Container / job ID |
| `TokenHash`, `TokenExpiresAt` | The run's credential (§6). Cleared on revocation. |
| `LogLocation` | Reserved for per-run logs (A8) |

The container receives only `--run-id`. `--application-id`, `--deployment-id` and `--operation` go away: the runner gets them from the run descriptor.

---

# 4. Runner lifecycle

```text
         Runner container started (--run-id, token in secrets.json)
                       │
                       ▼
       GET  /internal/runs/{runId}            ── RunDescriptor
                       │                          (operation, repo, commit, ids)
                       ▼
       Clone app repo at commit, parse score.yaml
                       │
                       ▼
       POST /internal/runs/{runId}/plan        ── ScoreSubmission → RunPlan
                       │                          (API resolves templates,
                       │                           records resources/instances)
                       ▼
       Load mapped secrets into the environment   (unchanged)
                       │
                       ▼
       Execute plan (terraform init/plan/apply or destroy)
                       │
              ┌────────┴────────┐
              ▼                 ▼
          Succeeded           Failed
              └────────┬────────┘
                       ▼
       POST /internal/runs/{runId}/complete    ── RunCompletion
                       │                          (API updates state, revokes token)
                       ▼
               Exit (0 / non-zero)
```

If anything fails before execution starts, the runner still calls `/complete` with `Failed` and exits non-zero.

---

# 5. Responsibility split

## Orchitect API

The API owns the control plane and answers the question:

> What should this run execute?

```text
API
├── Run lifecycle (DeploymentRun, Deployment status)
├── Run tokens (issue, validate, revoke)
├── Resource template resolution
├── Resource, instance and dependency-graph recording
├── Instance status transitions
└── Completion handling and exit-code fallback
```

The logic that `EngineOrchestrator` runs today against repositories (`BuildProvisionInputsAsync` template lookup, `RecordResourcesAsync`, `FindRecordedResourcesAsync`, `FindRemovableInstancesAsync`, `TransitionAsync`, `ReleaseResourcesAsync`) moves into an API-side service (#204).

## Runner

The runner owns execution and answers the question:

> How do I execute what I have been asked to execute?

```text
Runner
├── Fetch run descriptor
├── Clone app repo, parse score.yaml (ScoreDriver)
├── Submit score, receive plan
├── Load secrets into the process environment (unchanged, §9)
├── Download template modules, run provisioners
├── Capture output
├── Report completion
└── Return exit code
```

The runner has no repositories, no `Orchitect.Persistence` reference and no connection string (#105). Provisioners take contract `RunInput`s instead of domain `ResourceTemplate`s.

---

# 6. Authentication

Each run gets its own credential, kept as small as possible (#202):

- **Token.** The dispatch layer generates an opaque 256-bit random value when it queues the run. Only its SHA-256 hash is stored, on `DeploymentRun.TokenHash`.
- **Delivery.** The token is added to `/run/orchitect/secrets.json` (`RunnerSecretsFile`) in place of the connection string, so it doesn't show up in `docker inspect`. The runner loads the file and deletes it at startup, as it does today.
- **Lifetime.** `TokenExpiresAt = dispatch time + ExecutorOptions.Timeout + StopGracePeriod`. The run can never outlive that, because the executor kills it first.
- **Validation.** A dedicated `Runner` authentication scheme accepts the token only if all of these hold:
  - the hash matches a run, and that run is `Queued` or `Running`
  - the token hasn't expired
  - the route's `{runId}` equals the token's run
- **Revocation.** The hash is cleared as soon as the run completes, whether by the runner's report, the exit-code fallback or the sweep (§8). A token is therefore useless after its run, even if it leaks from a template's Terraform.
- **Isolation.**
  - Runner tokens can't call user endpoints, and user JWTs can't call `/internal/runs`.
  - The two schemes share no keys.

**Status (#202).** Implemented: `RunnerToken` (Dispatch) generates the token when `DeploymentQueue` starts the run, `DeploymentRun.IssueToken` stores the hash and expiry, and finishing the run in any way clears them. The token goes in `secrets.json` as `ORCHITECT_RUN_TOKEN`. The `Runner` scheme (`RunnerAuthenticationHandler`) and policy guard the `/internal/runs/{runId}` group (`MapRunnerGroup`), and the default policy accepts only user JWTs. Since #105 the token is the only credential for the API in `secrets.json`; the connection string is gone.

**Why not a JWT?** A JWT can't be revoked before it expires without a denylist, which needs a DB lookup anyway. It would also be signed with the same HMAC secret as user tokens (`JwtOptions:Secret`). A hashed opaque token gives revocation for free and adds no new key material.

---

# 7. Contract

All shared types live in `Orchitect.Engine.Contracts/Runner/Api/` (#203). The runner uses a typed `HttpClient` client in `Orchitect.Engine.Execution` that retries transient failures with backoff.

**Versioning.** Every request carries an `Orchitect-Runner-Contract: <n>` header. The API rejects a missing or mismatched version with a clear error, so a runner image built from a different commit fails fast instead of misbehaving (A1).

**Endpoints.** Route constants are in `RunnerRoutes`. All endpoints sit under `/internal/runs/{runId}`, require the `Runner` scheme, and are excluded from the public OpenAPI document.

| Method & path | Request | Response | Notes |
|---|---|---|---|
| `GET /` | – | `RunDescriptor` | Operation, application repo URL, commit, application/environment IDs. |
| `POST /plan` | `ScoreSubmission` | `RunPlan` | See below. Idempotent: a repeat call returns the stored plan. |
| `POST /complete` | `RunCompletion` | `204` | See §8. Idempotent: a repeat call for a completed run is a no-op. |

**`/plan` behaviour (#204):**

- **Provision:**
  - Resolve each score resource type to its template.
  - Get or create the `Resource` and `ResourceInstance`.
  - Update consumers and the dependency graph.
  - Move the instances to `Provisioning`.
- **Destroy:**
  - Find the recorded resources for the score's resources and their removable instances.
  - Plan each recorded resource from the template version its instance was provisioned with (`ResourceInstance.TemplateVersionId`), even if that version is no longer active (#213). A resource with nothing recorded uses the latest active version.
  - Move those instances to `Removing`.
- **Either:**
  - Return the `RunContext` (project name, application/environment IDs) and one `RunInput` per resource.
  - Each `RunInput` holds the key, template name and type, provider, version source URL/tag/path, and parameters.

**DTO sketch:**

```text
RunDescriptor   { RunId, Operation, RepositoryUrl, CommitId, ApplicationId, EnvironmentId }
ScoreSubmission { ScoreFile }                       // parsed score, contract shape
RunPlan         { Context: { ProjectName, ApplicationId, EnvironmentId }, Inputs: RunInput[] }
RunInput        { Key, TemplateName, TemplateType, Provider, Source: { BaseUrl, Tag, Path? }, Parameters }
RunCompletion   { Outcome: Succeeded | Failed, ErrorSummary? }
```

The round-trip test (#117) and the shared constants (#118) cover whatever env/arg contract is left (backend config, OTel).

**Status (#204).** Implemented:

- **Planner.** `IRunPlanner` (`Orchitect.Engine.Dispatch/Plan/`) holds the planning logic that was in `EngineOrchestrator`. `PlanAsync` resolves templates and records or finds the instances as above. It checks the score before it writes anything, and it runs in one transaction (`IUnitOfWork`) that first locks the run's row (`IDeploymentRunRepository.LockAsync`). So a rejected score leaves no records, and concurrent calls for one run take turns: the first records, the rest get the stored plan.
- **Completer.** `IRunCompleter` (`Orchitect.Engine.Dispatch/Completion/`) moves the planned instances to `Active` / `Failed` or `Removed` / `RemovalFailed`, and releases the resources after a successful destroy. It skips instances that have already finished, so it's safe to call twice. It's step 2 of §8's `CompleteRun`; #106 adds the rest.
- **Stored plan.** The plan is stored in `DeploymentRunPlans`, keyed by run ID, with each planned instance's ID and score key. A repeat `/plan` returns it without recording again, and logs a warning when the submitted score differs. The instance output (`Location` = module repository, `Workspace` = project name) is derived from the stored plan when the run completes, not stored with it. It's a placeholder until the runner reports real outputs.
- **Endpoint.** `POST /internal/runs/{runId}/plan` (`CreateRunPlanEndpoint`) maps the planner's `RunPlanException` failure: `404` for an unknown run, `409` (`RunNotRunning`) for a run that isn't `Running`, and `400` (`RunPlanInvalid`) for a missing score file, a score with no resources, an unknown resource type, a template with no active version (unless a destroy has a recorded version), or a resource recorded (or named twice in the score) with a different template.
- **Contract.** `RunInput` also carries `TemplateName`, which keeps the Terraform module names (and so the state addresses) unchanged, and `Provider` (`RunInputProvider`), which picks the provisioner. Renaming a template therefore changes its module addresses, and Terraform destroys and recreates its resources.
- **Runner.** Provisioners, drivers and validators take `RunInput`/`RunContext`, and Execution no longer references any resource repository. `EngineOrchestrator` parses the score, calls `IRunnerApiClient.SubmitScoreAsync`, executes the plan and reports through `CompleteAsync`. Until #105, the runner registered `InProcessRunnerApiClient`, which called `IRunPlanner` and `IRunCompleter` directly over its database connection.
- **Follow-ups.** These behaviours moved over unchanged and are now the API's to fix:
  - A provision records the new template version on an existing instance (`Reconfigure`) when it plans, not when the run succeeds. So after a failed upgrade from v1 to v2, the instance records v2 and a later destroy plans v2.
  - A resource shared through the score's `id` is moved to `Removing` and `Removed` by one consumer's destroy, even while other applications still consume it (#214).
  - The API trusts the submitted score. `Metadata.Name` becomes `RunContext.ProjectName`, which fills the backend's `{projectName}` placeholder, and `id` picks resource slugs across the environment. Once the runner is untrusted (#105, H1), the project name should come from the application and `id` claims should be checked (#215).

**Status (#203).** Implemented:

- **Contracts.** `RunnerRoutes`, `RunnerContract` (header name, version and the shared JSON options), `RunDescriptor`, `ScoreSubmission`, `RunPlan`, `RunContext`, `RunInput`, `RunInputSource`, `RunCompletion` and `RunOutcome` are in `Orchitect.Engine.Contracts/Runner/Api/`. The score models (`ScoreFile` and friends) moved to `Orchitect.Engine.Contracts/Score/` so `ScoreSubmission` can carry the parsed score.
- **Versioning.** `RunnerContractFilter` runs on every endpoint in `MapRunnerGroup` and returns `400` with the `RunnerContractMismatch` error code when the header is missing or different. Authentication runs first, so a request without a valid token still gets `401`.
- **Descriptor.** `GET /internal/runs/{runId}` (`GetRunDescriptorEndpoint`). `DeploymentQueue` already moves the run to `Running` when it issues the token, so the endpoint only reads.
- **Client.** `IRunnerApiClient` in `Orchitect.Engine.Execution`, registered by `AddRunnerApiClient(configuration)`. It reads `ORCHITECT_API_URL`, `ORCHITECT_RUN_ID` and `ORCHITECT_RUN_TOKEN`, sends the token and the contract header, and retries transient failures (5xx, 408, 429, network errors) with exponential backoff and jitter. Each attempt times out after 10s and the whole call after 2 minutes, so a hung request is retried rather than eating the budget. Missing or malformed settings fail options validation with a message naming the variable, at host start (`ValidateOnStart`). It replaces the default resilience handler from `AddServiceDefaults`, so retries don't stack. The runner uses it since #105.

---

# 8. Status reporting

Reporting is **run level only** (#106). Terraform applies the whole project at once, so there's no per-instance outcome to report yet. Progress events wait for the logs work (A8).

Every outcome goes through one API-side handler, `CompleteRun(runId, outcome, errorSummary, exitCode?)`. It:

1. Records the outcome, timestamps and exit code on the `DeploymentRun`.
2. Moves the instances: `Active` with output or `Failed` for a provision; `Removed` or `RemovalFailed` for a destroy. A successful destroy also releases the resources (removes the consumer and graph nodes).
3. Updates the `Deployment` (`Deployed` / `Destroyed` / `Failed`, with `ErrorSummary`).
4. Revokes the run token.

Three callers share that handler:

| Caller | When | Outcome source |
|---|---|---|
| `POST /complete` | The runner reports | The report. The exit code is recorded later but doesn't override it. |
| `DeploymentQueue` | The executor returns and the run is still `Running` (no report arrived) | Exit code, mapped by `RunResult.FromExitCode` |
| `RunnerContainerSweepService` | The API restarted mid-run | Exit code of the labelled container, or `Failed` when there is none |

So a run whose report never reaches the API (API down, network failure) still ends in a consistent state. The runner retries `/complete` with backoff before it exits.

**Status (#106).** Implemented:

- **Handler.** `IRunCompletionHandler` (Dispatch, `Completion/`) takes a `RunResult`: the outcome, an error summary, and the exit code and container ID when they are known. In one transaction it locks the run's row (`IDeploymentRunRepository.LockAsync`), re-reads the run and the deployment, and finishes the run (which revokes the token). It sets the `Deployment` only while the deployment is still active for this run's operation and this is its latest run. After the commit it calls `IRunCompleter` (#204) with the run's final outcome to move the planned instances and, after a destroy, release the resources.
- **Stale run writes.** These are gone. The handler always works from the run it re-read under the lock, so a report and the exit-code fallback that race take turns, and the second one only records its exit code. On a `DeploymentRunConflictException` from a writer that doesn't lock, such as a cancel request, it retries once with a fresh read.
- **Repeat calls.** For a run that has already completed, the handler only fills in a missing exit code and container ID, and returns `false`. A repeat `POST /complete` never reaches the handler, because completion revoked the token: it gets `401`.
- **Cancellation.** It stays outside the handler. When a stopped runner exits non-zero and the run is still active, the queue and the sweep cancel it as before (#210). If the runner already reported, the report wins.
- **Endpoint.** `POST /internal/runs/{runId}/complete` (`CompleteRunEndpoint`) returns `204`, or `400` with `InvalidRunOutcome` for an outcome outside `Succeeded`/`Failed`.
- **Runner.** `EngineOrchestrator` reports through `IRunnerApiClient.CompleteAsync`. A runner that crashes before reporting is settled by the exit-code fallback, so its instances no longer stay `Provisioning` or `Removing`.

---

# 9. Secrets

Secret injection doesn't change. There is no secrets endpoint on the runner API.

- **Secrets file.** The API still writes `/run/orchitect/secrets.json` with `ExecutorOptions:Configuration` and, for `AzureKeyVault`, the Key Vault token. The only change is that the run token replaces the connection string (§6).
- **Runner-side resolution.** The runner still resolves the `SecretProvider:Mappings` entries through its own `ISecretProvider` and loads them into its process environment before any Terraform command (`SecretEnvironmentLoader`).
- **Non-secret config.** `SecretProvider__*` settings still travel as container env, built by `ExecutorOptions.ToEnvironment()`.

H2 (the Key Vault token covers every secret the API's identity can read) is therefore not addressed by this design and stays open (#108).

---

# 10. Network

The runner needs to reach the API, and only the API (#107):

- **`ApiBaseUrl`.** A new `ExecutorOptions:ApiBaseUrl` is the URL as the runner sees it. It's rewritten for `host-gateway` the same way `DockerExecutor.RewriteLoopbackUrl` handles the OTLP endpoint, and validated at startup.
- **No database settings.** `DatabaseHost`, `DatabasePort` and `RewriteConnectionString` are removed. The runner has no route to Postgres.
- **Optional hardening.** Serve `/internal/runs` on a separate Kestrel listener that only the runner network can reach. This isn't required for the first version.

---

# 11. Migration order

| Step | Change | Issue |
|---|---|---|
| 1 | `DeploymentRun` entity, run ID used for containers | #110 |
| 2 | Per-run token and `Runner` auth scheme | #202 |
| 3 | Contracts, run descriptor endpoint, runner client | #203 |
| 4 | `/plan`: template resolution and resource recording move to the API | #204 |
| 5 | `/complete` and the shared `CompleteRun` handler | #106 |
| 6 | Runner switched to the client; Persistence reference and connection string removed | #105 |
| 7 | `ApiBaseUrl` replaces the DB host/port settings | #107 |

Steps 1–5 can ship while the runner still uses the database. Step 6 is the switch-over. After it, H1 and A1 are closed.

**Status (#105).** Implemented:

- **Runner.** The container takes only `--run-id`. `Program.cs` parses it, then builds the host with `AddEngineProvisioningServices`, `AddRunnerServices` and `AddRunnerApiClient`, so `--help` and argument errors don't touch configuration. `IEngineOrchestrator.RunAsync` follows §4: it fetches the descriptor, has `ScoreDriver` clone `RepositoryUrl` at `CommitId` and parse `score.yaml`, submits it, loads the mapped secrets, runs `Provision` or `Destroy` from the descriptor's `Operation`, and reports. A failure at any of those steps is reported as `Failed` before the runner exits non-zero.
- **References.** `Orchitect.Runner` references only `Orchitect.Engine.Execution` and `Orchitect.ServiceDefaults`, and `Orchitect.Engine.Execution` no longer references `Orchitect.Domain`. `InProcessRunnerApiClient` is deleted, and the Dockerfile copies only the projects the runner builds from. `EngineLayeringTests` fails if the runner references Dispatch, Persistence or Domain.
- **Dispatch.** `DeploymentQueue` passes `--run-id` and `ExecutorOptions:ApiBaseUrl`. `DockerExecutor` drops the connection string from `secrets.json` and sets `ORCHITECT_API_URL`, rewriting a loopback host to `host.docker.internal` as it does for the OTLP endpoint (`RewriteLoopbackUrl`). It refuses to start a runner when `ApiBaseUrl` isn't set.
- **AppHost.** Sets `ExecutorOptions__ApiBaseUrl` to the API's own `http` endpoint and binds that endpoint to `0.0.0.0`. On Linux, `host-gateway` can't reach a listener bound to `127.0.0.1` only, which is where DCP binds endpoints by default. As a side effect, the dev API is reachable from the LAN while the AppHost runs.
- **Left for #107.** `DatabaseHost`, `DatabasePort` and `RewriteConnectionString` are now unused. Startup validation of `ApiBaseUrl` is also still to do.

**Status (#107).** Done: `DatabaseHost`, `DatabasePort` and `RewriteConnectionString` are removed (and the AppHost no longer sets them), and `ExecutorOptions:ApiBaseUrl` must be an absolute `http` or `https` URL at startup (`ValidateOnStart`). The separation is complete.

---

# 12. Out of scope

- Per-run logs and progress streaming (A8, #121).
- Durable queue and bounded concurrency (H3, #111).
- Changes to secret injection: narrowing the Key Vault token (H2, #108) and secret-provider strategies (A6, #109).
- Executors other than Docker (H4, #114). The contract doesn't assume Docker: any executor that can start an image with a run ID, a secrets file and a route to the API works.
