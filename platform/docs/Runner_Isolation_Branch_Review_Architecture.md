# Runner Isolation Branch Review: Architecture

Architectural review of `feature/runner_isolation` against `master`, 2026-09-25.

This complements [Runner_Isolation_Branch_Review.md](Runner_Isolation_Branch_Review.md), which covers code-level findings (E*, M*, H* references below point to it).

Severity: 🔴 high, 🟡 medium, 🟢 low.

## Summary

| # | Finding | Severity |
|---|---|---|
| A1 | The branch works against the target architecture in `API_RUNNER_SEPARATION.md` (runner has DB access) | 🔴 |
| A2 | No "Run" concept in the domain | 🔴 |
| A3 | `Orchitect.Infrastructure.Engine` mixes the control plane and the data plane | 🟡 |
| A4 | The API–runner contract is implicit and untested | 🟡 |
| A5 | The executor knows about the database | 🟡 |
| A6 | Adding a secret provider means editing switches in separate places | 🟡 |
| A7 | Local state mode is the default but broken in this topology | 🟡 |
| A8 | Runner observability: no trace propagation, no per-run logs | 🟡 |
| A9 | An architecture guard test was silently broken | 🟢 |
| A10 | Docs have drifted | 🟢 |

## What the branch gets right

- **The split in principle.** Provisioning now runs in a disposable container, and the API only dispatches it. This removes the old problem of Terraform, Azure CLI and `ARM_*` credentials being built into the API image.
- **`IExecutor` is a good seam.** Swapping Docker for Kubernetes Jobs, ACA Jobs or ECS later only means adding an implementation.
- **Terraform state is pluggable.** Any backend works through a partial `backend {}` block plus `-backend-config`, with no Azure-specific code.
- **Secret sources are pluggable.** `ISecretProvider` with Environment and Key Vault implementations, selected by config and validated at startup.
- **The project split.** Breaking `Orchitect.Infrastructure` into `.Engine` and `.Inventory`, with unit test projects for each, is the right direction.

---

## A1. 🔴 The branch works against its own target architecture

`docs/API_RUNNER_SEPARATION.md` says the runner should **not have direct access to the Orchitect database** and should talk to the API through an authenticated internal API. This branch does the opposite:

- `Orchitect.Runner` references `Orchitect.Persistence` and receives the API's connection string (`DockerExecutor.cs:57`).
- `AddPersistenceServices` registers every repository in the runner, including `Credential`, `User` and `Organisation`.

The consequences go beyond security (see H1):

- **Schema coupling.** The runner image carries an EF model. If the API and the runner are built from different commits, the runner breaks against a migrated schema. The image tag is the floating `orchitect-runner:terraform`, so version skew is likely.
- **The runner decides what to run.** It parses the score file and resolves templates itself. The target doc says that's the API's job ("What should this run execute?").

**Recommendation:** decide whether this branch is an intermediate step.
- If it is, say so in `API_RUNNER_SEPARATION.md`, and design the run manifest contract now, even if the DB path stays for a while.
- If it isn't, the doc and the code need to agree.

In either case, tie the runner image tag to the API build version.

## A2. 🔴 There's no "Run" in the domain

Deployment ID and run are the same thing: `RunId = DeploymentId` (`DeploymentQueue.cs:35`), and the container is named after the deployment. Several open issues come from that one missing concept:

- Status is never recorded (M3), and there's no exit code, timestamps or log pointer.
- A deployment can't be retried, and the container name collides (E9).
- Destroy can't be modelled: `--operation` exists in the runner, but `DeploymentQueueRequest` has no operation field.
- The queue can't be durable (H3), because there's nothing to persist.

**Recommendation:** add a `DeploymentRun` entity:

| Field | Purpose |
|---|---|
| `Id` | Run identity, used for container name, logs and tracing |
| `DeploymentId` | Parent deployment |
| `Operation` | `Provision` / `Destroy` |
| `Status` | `Queued` / `Running` / `Succeeded` / `Failed` / `Cancelled` |
| `QueuedAt`, `StartedAt`, `FinishedAt` | Lifecycle timestamps |
| `ExitCode` | Runner exit code |
| `RunnerId` | Container / job ID |
| `LogLocation` | Pointer to stored run logs (A8) |

The queue then persists runs instead of holding closures in memory, status updates attach to runs, and destroy becomes just another operation. This is the foundation for M3, H3 and E9, so it's the single change with the most leverage.

## A3. 🟡 `Orchitect.Infrastructure.Engine` mixes the control plane and the data plane

The API and the runner share one assembly that contains two different roles:

| API only (control plane) | Runner only (data plane) |
|---|---|
| `Executor/*`, Docker.DotNet | Terraform, Helm, Score drivers |
| `Queue/*`, `QueuedHostedService` | `SecretEnvironmentLoader`, `ISecretProvider` |
| `KeyVaultRunnerTokenProvider` | `EngineOrchestrator`, provisioners |
| `Encryption/*` (actually a Core concern) | |

Consequences:

- `AddEngineInfrastructureServices` registers everything in both processes (M8).
- The API carries Terraform drivers it can't run, since its image has no Terraform binary.
- The runner image ships the Docker client and the queue.
- Nothing stops API code from calling `ITerraformDriver` in-process. The Playground does exactly that, so there are two execution paths to keep behaving the same.

**Recommendation:** split it into three projects:

```text
Orchitect.Engine.Contracts    run manifest, env key names, RunnerOperation
        ▲             ▲
        │             │
Orchitect.Engine.Dispatch      Orchitect.Engine.Execution
(API: executor, queue,         (Runner: orchestrator, provisioners,
 token minting)                 drivers, secret loading)
```

Also move `AesEncryptionService` and `EncryptionOptions` back to a Core or shared project. Credentials belong to Core, not to Engine.

## A4. 🟡 The API–runner contract is implicit and untested

The whole interface is CLI arguments plus env var names assembled from strings in several places:

- `ExecutorOptions.ToEnvironment()` (`TerraformBackend__Config__…`, `SecretProvider__Mappings__…`)
- the `KeyVaultRunnerTokenProvider` consts (`AccessTokenKey`, `AccessTokenExpiresOnKey`)
- `ORCHITECT_RUN_ID` and `ConnectionStrings__orchitect` in `DockerExecutor`
- the option names in the runner's `Program.cs` (`--application-id`, `--deployment-id`, `--operation`)

`ExecutorOptionsTests` checks which keys are produced. Nothing checks that those keys **bind back** into the runner's `TerraformBackendOptions` and `SecretProviderOptions`. A rename on either side compiles fine and fails only at runtime inside a container.

**Recommendation:**
- Put the key names and argument names in `Orchitect.Engine.Contracts` (A3).
- Add a round-trip test: `ExecutorOptions.ToEnvironment()` → `ConfigurationBuilder().AddInMemoryCollection()` → `AddRunnerServices` / bind → assert on the resulting options.
- Longer term, replace most of the env vars with a single versioned run-manifest file mounted into the container.

## A5. 🟡 The executor knows about the database

`ExecutorContext` carries `DatabaseHost` and `DatabasePort`, and `DockerExecutor.BuildRunnerConnectionString` rewrites a Postgres connection string. That's a composition concern leaking into what should be a generic "run this image with this config" abstraction. A Kubernetes or ACA executor would have to repeat the same logic.

**Recommendation:** build the runner's configuration in one place (the dispatch layer, currently `DeploymentQueue`) and keep `ExecutorContext` to image, arguments, environment and resource limits. If A1 goes the manifest route, this goes away entirely.

## A6. 🟡 Adding a secret provider means editing switches in separate places

`SecretProviderType` is switched on in:

- `SecretProviderOptions.GetValidationError`
- `EngineInfrastructureExtensions.AddRunnerServices` (runner-side provider selection)
- `KeyVaultRunnerTokenProvider` (API-side, which returns an empty result unless the type is Key Vault)

`IRunnerSecretTokenProvider` has a generic name but a single Key Vault implementation, and it's registered unconditionally. Adding AWS Secrets Manager means editing all of these.

**Recommendation:** use one strategy per provider type, holding both halves ("what the API hands over" and "how the runner resolves"), registered by type. Note that H2 may change this anyway: if the API resolves the mapped secrets itself, the runner-side provider selection mostly disappears.

## A7. 🟡 Local state mode is the default but broken in this topology

`appsettings.json` defaults to `TerraformBackend:Mode = Local`, and the runner container is removed after each run. Every deployment with the default config therefore creates infrastructure whose state is then deleted:

- resources are orphaned
- the next run tries to create them again
- destroy can't work

A warning is logged, but the combination is invalid, not just risky.

**Recommendation:** reject `Local` in `ExecutorOptions` validation when the executor is Docker, unless a state volume is mounted. Keep `Local` for the in-process Playground path.

## A8. 🟡 Runner observability

- The runner clears its log providers and writes JSON to stdout. The API parses that and re-logs it (`ExecutorOutputRelay`). That works, but it makes the API a log aggregator for every runner, and the logs aren't stored per run, so users can't see them.
- There's no trace propagation. The API creates an activity for the run, but no `traceparent` / `OTEL_*` env vars are passed in, and the runner has no OTel exporter, so the runner's spans (`Tracing.StartActivity()` throughout the drivers) go nowhere.

**Recommendation:**
- Pass `TRACEPARENT` and the OTLP endpoint into the container, and let the runner use the ServiceDefaults OTel setup.
- Store run logs against the `DeploymentRun` (A2), either as a blob or in a table, so they can be shown through the API or portal.

## A9. 🟢 An architecture guard test was silently broken

`Orchitect.Infrastructure.Inventory.Unit.Tests/NamespaceUsageTests.cs` only checks namespaces starting with `Orchitect.Infrastructure.Inventory.Inventory`, a prefix left over from the rename. The real namespaces are now `Orchitect.Infrastructure.Inventory.Azure…`, `….GitHub…` and so on, so nothing matches and the test always passes. The module-isolation rule for the Inventory providers isn't enforced any more.

**Fix:**
- Update the prefix and the `AllowedNamespaces` entries.
- Once A3 is done, add similar tests for Engine, for example "Execution must not reference Dispatch".

## A10. 🟢 Docs have drifted

- `CLAUDE.md` still describes `Orchitect.Core.Api`, `Orchitect.Engine.Api`, `Orchitect.Inventory.Api` and per-context persistence projects, none of which exist.
- `RUNNER_TODO.md` uses the old names: `DockerRunner`, `RunnerOptions`, `docker-configure.sh` (see E14).
- `API_RUNNER_SEPARATION.md` describes the opposite of the current design (A1).

These docs are how humans and agents navigate the repo, so it's worth one pass to bring them in line.

---

## Priorities

1. **Decide on the DB question (A1).** It determines what A4 and A5 look like and whether H1 and H2 are fixed properly.
2. **Add the `DeploymentRun` entity (A2).** It unblocks status, retries, destroy and a durable queue.
3. **Split Engine into Contracts, Dispatch and Execution (A3), and add the round-trip contract test (A4).** These are mostly mechanical once 1 and 2 are settled.
4. **Quick wins now:**
   - reject `Local` state for the Docker executor (A7)
   - fix the namespace guard test (A9)
   - pass trace context into the runner (A8)
   - update the docs (A10)
