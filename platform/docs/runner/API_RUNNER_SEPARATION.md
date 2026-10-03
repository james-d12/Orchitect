---
title: "API / runner separation (target design)"
status: active
workstream: runner
milestone: "Runner Isolation"
issues: [102, 103, 105, 106, 107, 110, 202, 203, 204]
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

**Status (#202).** Implemented: `RunnerToken` (Dispatch) generates the token when `DeploymentQueue` starts the run, `DeploymentRun.IssueToken` stores the hash and expiry, and finishing the run in any way clears them. The token goes in `secrets.json` as `ORCHITECT_RUN_TOKEN`. The `Runner` scheme (`RunnerAuthenticationHandler`) and policy guard the `/internal/runs/{runId}` group (`MapRunnerGroup`), and the default policy accepts only user JWTs. The connection string stays in `secrets.json` alongside the token until the runner stops using the database (#105).

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
  - Move those instances to `Removing`.
- **Either:**
  - Return the `RunContext` (project name, application/environment IDs) and one `RunInput` per resource.
  - Each `RunInput` holds the key, template type, version source URL/tag, and parameters.

**DTO sketch:**

```text
RunDescriptor   { RunId, Operation, RepositoryUrl, CommitId, ApplicationId, EnvironmentId }
ScoreSubmission { ScoreFile }                       // parsed score, contract shape
RunPlan         { Context: { ProjectName, ApplicationId, EnvironmentId }, Inputs: RunInput[] }
RunInput        { Key, TemplateType, Source: { BaseUrl, Tag, Path? }, Parameters }
RunCompletion   { Outcome: Succeeded | Failed, ErrorSummary? }
```

The round-trip test (#117) and the shared constants (#118) cover whatever env/arg contract is left (backend config, OTel).

**Status (#203).** Implemented:

- **Contracts.** `RunnerRoutes`, `RunnerContract` (header name, version and the shared JSON options), `RunDescriptor`, `ScoreSubmission`, `RunPlan`, `RunContext`, `RunInput`, `RunInputSource`, `RunCompletion` and `RunOutcome` are in `Orchitect.Engine.Contracts/Runner/Api/`. The score models (`ScoreFile` and friends) moved to `Orchitect.Engine.Contracts/Score/` so `ScoreSubmission` can carry the parsed score.
- **Versioning.** `RunnerContractFilter` runs on every endpoint in `MapRunnerGroup` and returns `400` with the `RunnerContractMismatch` error code when the header is missing or different. Authentication runs first, so a request without a valid token still gets `401`.
- **Descriptor.** `GET /internal/runs/{runId}` (`GetRunDescriptorEndpoint`). `DeploymentQueue` already moves the run to `Running` when it issues the token, so the endpoint only reads.
- **Client.** `IRunnerApiClient` in `Orchitect.Engine.Execution`, registered by `AddRunnerApiClient(configuration)`. It reads `ORCHITECT_API_URL`, `ORCHITECT_RUN_ID` and `ORCHITECT_RUN_TOKEN`, sends the token and the contract header, and retries transient failures (5xx, 408, 429, network errors) with exponential backoff and jitter. Each attempt times out after 10s and the whole call after 2 minutes, so a hung request is retried rather than eating the budget. Missing or malformed settings fail options validation with a message naming the variable, at host start (`ValidateOnStart`). It replaces the default resilience handler from `AddServiceDefaults`, so retries don't stack. The runner doesn't use it yet (#105), and nothing sets `ORCHITECT_API_URL` until #107.

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
| `DeploymentQueue` | The executor returns and the run is still `Running` (no report arrived) | Exit code, using today's `ProcessDeploymentStatus` mapping |
| `RunnerContainerSweepService` | The API restarted mid-run | Exit code of the labelled container, or `Failed` when there is none |

So a run whose report never reaches the API (API down, network failure) still ends in a consistent state. The runner retries `/complete` with backoff before it exits.

**Status (#106).** Implemented:

- **Handler.** `IRunCompletionHandler` (Dispatch, `Completion/`) takes a `RunResult`: the outcome, an error summary, and the exit code and container ID when they are known. It claims the run with `IDeploymentRunRepository.TryFinishAsync`, a conditional update that only succeeds while the run is still `Queued` or `Running`. Only the caller that claims the run moves its instances, so a report and the exit-code fallback that race can't both settle them. The handler updates the `Deployment` only while it is still active for this run's operation and this is the deployment's latest run.
- **Repeat calls.** For a run that has already completed, the handler only fills in a missing exit code and container ID, and returns `false`. A repeat `POST /complete` never reaches the handler, because completion revoked the token: it gets `401`.
- **Which instances.** Until #204 moves recording into the API, `EngineOrchestrator` in the runner records the run's plan on `DeploymentRun` (`ProjectName` and `InstanceIds`) before it moves the instances to `Provisioning` or `Removing`. The handler settles only those instances that are still in flight. The instance output's workspace comes from `ProjectName`. The runner no longer makes the final transitions or releases resources itself, so a runner that crashes mid-run no longer leaves instances stuck in `Provisioning`.
- **Endpoint.** `POST /internal/runs/{runId}/complete` (`CompleteRunEndpoint`) returns `204`, or `400` with `InvalidRunOutcome` for an outcome outside `Succeeded`/`Failed`.
- **Runner.** `IRunCompletionReporter` wraps the run, reports `Succeeded`, or `Failed` with the exception message, through `IRunnerApiClient.CompleteAsync` (which retries with backoff), and rethrows so the exit code still matches. A report that can't be delivered is only logged. The runner reports only when `ORCHITECT_API_URL` is set, and nothing sets it until #107. Until then every run ends through the exit-code fallback.

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

- **`ApiBaseUrl`.** A new `ExecutorOptions:ApiBaseUrl` is the URL as the runner sees it. It's rewritten for `host-gateway` the same way `DockerExecutor.RewriteOtlpEndpoint` handles the OTLP endpoint, and validated at startup.
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

---

# 12. Out of scope

- Per-run logs and progress streaming (A8, #121).
- Durable queue and bounded concurrency (H3, #111).
- Changes to secret injection: narrowing the Key Vault token (H2, #108) and secret-provider strategies (A6, #109).
- Executors other than Docker (H4, #114). The contract doesn't assume Docker: any executor that can start an image with a run ID, a secrets file and a route to the API works.
