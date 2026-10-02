---
title: "API / runner separation (target design)"
status: active
workstream: runner
milestone: "Runner Isolation"
issues: [102, 103, 105, 106, 107, 108, 109, 110, 202, 203, 204]
superseded_by: null
last_reviewed: 2026-10-02
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
                         │  Secret resolution       │
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

The runner API needs a stable run ID (#110). Today `RunId = DeploymentId`, but one deployment can have several runs (provision, destroy, retries), and the token and every endpoint below are scoped to a single run.

`DeploymentRun` is a prerequisite:

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
       GET  /internal/runs/{runId}/secrets     ── RunSecrets (read once)
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
├── Secret resolution (mapped secrets only)
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
├── Load secrets into the process environment
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
- **Delivery.** The token is the only entry in `/run/orchitect/secrets.json` (`RunnerSecretsFile`), so it doesn't show up in `docker inspect`. The runner loads the file and deletes it at startup, as it does today.
- **Lifetime.** `TokenExpiresAt = dispatch time + ExecutorOptions.Timeout + StopGracePeriod`. The run can never outlive that, because the executor kills it first.
- **Validation.** A dedicated `Runner` authentication scheme accepts the token only if all of these hold:
  - the hash matches a run, and that run is `Queued` or `Running`
  - the token hasn't expired
  - the route's `{runId}` equals the token's run
- **Revocation.** The hash is cleared as soon as the run completes, whether by the runner's report, the exit-code fallback or the sweep (§8). A token is therefore useless after its run, even if it leaks from a template's Terraform.
- **Isolation.**
  - Runner tokens can't call user endpoints, and user JWTs can't call `/internal/runs`.
  - The two schemes share no keys.

**Why not a JWT?** A JWT can't be revoked before it expires without a denylist, which needs a DB lookup anyway. It would also be signed with the same HMAC secret as user tokens (`JwtOptions:Secret`). A hashed opaque token gives revocation for free and adds no new key material.

---

# 7. Contract

All shared types live in `Orchitect.Engine.Contracts/Runner/Api/` (#203). The runner uses a typed `HttpClient` client in `Orchitect.Engine.Execution` that retries transient failures with backoff.

**Versioning.** Every request carries an `Orchitect-Runner-Contract: <n>` header. The API rejects a missing or mismatched version with a clear error, so a runner image built from a different commit fails fast instead of misbehaving (A1).

**Endpoints.** Route constants are in `RunnerRoutes`. All endpoints sit under `/internal/runs/{runId}`, require the `Runner` scheme, and are excluded from the public OpenAPI document.

| Method & path | Request | Response | Notes |
|---|---|---|---|
| `GET /` | – | `RunDescriptor` | Operation, application repo URL, commit, application/environment IDs. Moves the run to `Running`. |
| `POST /plan` | `ScoreSubmission` | `RunPlan` | See below. Idempotent: a repeat call returns the stored plan. |
| `GET /secrets` | – | `RunSecrets` | Mapped secrets only (§9). Read once; a second read returns `410 Gone`. |
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
  - Return the `ProvisionContext` (project name, application/environment IDs) and one `RunInput` per resource.
  - Each `RunInput` holds the key, template type, version source URL/tag, and parameters.

**DTO sketch:**

```text
RunDescriptor   { RunId, Operation, RepositoryUrl, CommitId, ApplicationId, EnvironmentId }
ScoreSubmission { ScoreFile }                       // parsed score, contract shape
RunPlan         { Context: { ProjectName, ApplicationId, EnvironmentId }, Inputs: RunInput[] }
RunInput        { Key, TemplateType, Source: { BaseUrl, Tag, Path? }, Parameters }
RunSecrets      { Values: { name → value } }
RunCompletion   { Outcome: Succeeded | Failed, ErrorSummary? }
```

The round-trip test (#117) and the shared constants (#118) cover whatever env/arg contract is left (backend config, OTel).

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

---

# 9. Secrets

Secrets are resolved in the API and handed over narrowly (#108). This also fixes H2.

- **API-side resolution.** The API resolves only the `SecretProvider:Mappings` entries plus `ExecutorOptions:Configuration`, using its own identity.
- **Delivery.** `GET /secrets` returns them once. The runner loads them into its process environment before any Terraform command, as `SecretEnvironmentLoader` does today.
- **No Key Vault token.** The runner no longer gets one, and `KeyVaultRunnerTokenProvider` is removed.
- **Providers move to dispatch.** `ISecretProvider` and its implementations move from Execution to Dispatch. The provider-type switches then exist only on the API side (#109).
- **No logging.** Secret values never appear in logs, traces or exception messages.

Template Terraform can still read whatever is in the runner's environment. That's inherent to running Terraform, but the exposure is now limited to this run's mapped secrets.

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
| 6 | `/secrets` with API-side resolution | #108 |
| 7 | Runner switched to the client; Persistence reference and connection string removed | #105 |
| 8 | `ApiBaseUrl` replaces the DB host/port settings | #107 |
| 9 | Secret-provider strategies, API side only | #109 |

Steps 1–6 can ship while the runner still uses the database. Step 7 is the switch-over. After it, H1 and A1 are closed.

---

# 12. Out of scope

- Per-run logs and progress streaming (A8, #121).
- Durable queue and bounded concurrency (H3, #111).
- Executors other than Docker (H4, #114). The contract doesn't assume Docker: any executor that can start an image with a run ID, a secrets file and a route to the API works.
