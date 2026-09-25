# Runner Isolation – Fix Plan (Easy + Medium)

## Context
`platform/docs/Runner_Isolation_Branch_Review.md` reviewed `feature/runner_isolation` and found 15 Easy and 10 Medium issues. They cover Terraform error handling, HCL injection, secrets exposure, container lifecycle and deployment status. The Hard items (H1–H4) are out of scope and will be tracked in `docs/RUNNER_TODO.md`.

**How the work is delivered:**
- **Phase 1** fixes all the Easy findings (E1–E15). Then it **stops for review**.
- **Phases 2–11** fix one Medium finding each (M1–M10), in order. Each phase is one commit and **stops for review** before the next one starts.

Decisions made:
- **M1** uses root variables plus `terraform.tfvars.json`, so values are never evaluated as code.
- **M7** copies a secrets file into the container with put-archive before it starts.

Paths are relative to `platform/src/Orchitect.Infrastructure.Engine/` unless they say otherwise.

Comment policy: only short summary docs on interfaces, no other code comments.

Every phase ends with the same checks:
- `dotnet build` (warnings are errors)
- `dotnet test src/Orchitect.Infrastructure.Engine.Unit.Tests`
- the full `dotnet test`
- the phase-specific checks listed under it

---

## Phase 1 – All Easy fixes (E1–E15) → review

**Terraform command line** (`Provisioner/Terraform/TerraformCommandLine.cs`)
- E5: build every command, including config-inspect and validate, with `WithArguments(IEnumerable<string>)`.
- E6:
  - Add `-lock-timeout=5m` to plan, plan-destroy and apply.
  - Add `-input=false` to apply.
  - Remove `RunDestroyAsync` from the interface and the class.

**Terraform driver** (`Provisioner/Terraform/TerraformDriver.cs`)
- E1: add an explicit `case ChangesNeeded` that returns `Success`. The `default` returns `PlanFailed` and logs the unexpected exit code.
- E6: `DestroyAsync` calls `RunApplyAsync`.
- E7: set the `PreValidationFailed` message to the joined `"{template}: {message}"` of each invalid result.

**Terraform validator** (`Provisioner/Terraform/TerraformValidator.cs` and `Models/TerraformConfig.cs`)
- E3:
  - Add `Diagnostics` (severity, summary, detail, pos) to `TerraformConfig`.
  - Always try to deserialize stdout, whatever the exit code.
  - Catch `JsonException`, and include stdout and stderr in the `ModuleInspection` error.
  - Error-severity diagnostics become a formatted `ModuleInvalid` message.
- E13:
  - Match inputs to variables with `Ordinal`.
  - Replace the recursive `variables.tf`/`outputs.tf` check with a check for at least one `*.tf` file in the module root (`TopDirectoryOnly`).

**Orchestrator** (`EngineOrchestrator.cs`)
- E12: a null score file, or null/empty `Resources`, throws.
- E2:
  - A missing resource template throws, and the message names the type and the resource key.
  - A null `Parameters` becomes an empty dictionary.
- The return type becomes non-nullable.

**Docker executor** (`Executor/DockerExecutor.cs`, `ExecutorOptions.cs`, `IExecutor.cs`, `Queue/DeploymentQueue.cs`)
- E4: add `ExecutorOptions.LogLevel` (default `Information`) and emit it in `ToEnvironment()`. Remove the hardcoded `Debug`.
- E8:
  - Set `HostConfig` to `CapDrop = ["ALL"]`, `SecurityOpt = ["no-new-privileges"]` and `Init = true`.
  - Add `Memory`, `NanoCPUs` and `PidsLimit` from the new nullable `ExecutorOptions` values (`MemoryBytes`, `NanoCpus`, `PidsLimit`), passed through `ExecutorContext`.
- E9:
  - Name the container `orchitect-runner-{RunId}-{8-char guid}`.
  - Add the labels `orchitect.runner=true` and `orchitect.run-id={RunId}`.
- E10:
  - Inject `IConfiguration` and use `GetConnectionString("orchitect")`.
  - Rewrite the connection string with `NpgsqlConnectionStringBuilder`. Add a `Npgsql` reference if it isn't there transitively.
- E11:
  - Catch `OperationCanceledException` when the token is cancelled, before the general catch, and log it at Debug.
  - Observe the pending `read` task.
  - Briefly await `logStreaming` after a drain-timeout cancel.
  - Log `DockerContainerNotFoundException` at Warning.

**Config and docs**
- E14 (`EngineInfrastructureExtensions.cs`):
  - Change `RunnerOptions:` to `ExecutorOptions:` in the validation messages.
  - Add `[Required]` and a non-blank `.Validate` for `Image`.
- E14 (`docs/RUNNER_TODO.md`):
  - Rename `DockerRunner`/`RunnerOptions` to the current names.
  - Remove `docker-configure.sh` and `ORCHITECT_CLOUD_PROVIDER`.
  - Fix the build command and tag.
  - Tick the §7 items that are done.
  - Add H1–H4 as tracked items.

**Runner image** (`Orchitect.Runner/Dockerfile`, E15)
- Pin `alpine` and `golang` to their current minor versions.
- Verify Terraform with GPG before checking `SHA256SUMS`:
  - `apk add gnupg`
  - import the HashiCorp key and check its fingerprint against HashiCorp's security page
  - `gpg --verify` the `.sig`
- Note in the header comment that BuildKit is required.

**Tests**
- Driver: exit codes 2, 137 and -1. The pre-validation message contains the reasons. Destroy uses `RunApplyAsync`.
- Validator:
  - diagnostics with exit 1
  - malformed JSON fails only its own template
  - case mismatch → `InputInvalid`
  - `variables.tf` only under `examples/`
- A new orchestrator test for a missing template, null parameters and an empty score.
- `ExecutorOptionsTests` covers the log level.
- A connection-string rewrite test with `Server=localhost`.

**Extra checks**
- `docker build -f src/Orchitect.Runner/Dockerfile -t orchitect-runner:terraform .` from `platform/`.
- A run through Aspire and Bruno. `docker inspect` shows the labels, `CapDrop` and `Init`, and the runner logs arrive at Information.

---

## Phase 2 – M1: HCL injection → review
- Add a new `Provisioner/Terraform/TerraformValueConverter.cs`. It converts a raw string into a `JsonNode` based on the module variable's type:
  - `string` → string
  - `number` → invariant decimal
  - `bool` → bool
  - collections and objects → parsed as JSON, with a `'`→`"` fallback
  - `any`/null → today's heuristic
- The validator calls the converter, so a bad value becomes `InputInvalid`.
- `TerraformRenderer.RenderModules` builds its output with `System.Text.Json.Nodes` and returns `(MainTfJson, TfVarsJson)`:
  - Module names are sanitized.
  - Each input gets a root `variable "{module}__{input}"`, with its type copied from the module.
  - Module arguments are `"${var.…}"`.
  - The values go in the tfvars.
- The providers and backend blocks are also rendered as JSON.
- `TerraformProjectBuilder` writes `main.tf.json`, `terraform.tfvars.json`, `providers.tf.json` and `backend.tf.json`. It never logs the tfvars.
- Tests:
  - An injection payload (`"\n}\nresource…` or `${file(...)}`) appears only in the tfvars, verbatim.
  - Types come through correctly.
  - The converter has its own tests.
- Check: a run with a `${file("/etc/passwd")}` value is applied literally.

## Phase 3 – M2: cancellation and SIGINT forwarding → review
- `CommandLineBuilder.ExecuteAsync`/`ExecuteStreamAsync(CancellationToken)`:
  - On cancel, send SIGINT through a `libc kill` P/Invoke. On Windows, use `Kill(true)`.
  - Wait up to a 5-minute grace period, then `Kill(true)` and throw `OperationCanceledException`.
- Thread `CancellationToken` through `ITerraformCommandLine`, `TerraformValidator`, `TerraformDriver` and `TerraformProvisioner`. The provisioner currently ignores its token.
- `Orchitect.Runner/Program.cs`: invoke with `ProcessTerminationTimeout = null`.
- `DockerExecutor`: set `StopTimeout` from a new `ExecutorOptions.StopGracePeriod` (default 5 min).
- Test: `CommandLineBuilder` cancel against a `sleep`/trap script records the SIGINT.
- Check: `docker stop` on a running runner, and Terraform logs that it's interrupted and releases the lock.

## Phase 4 – M3: deployment status → review
- `DeploymentStatus`: add `Deploying`. It's stored as a string, so no migration is needed.
- `Deployment.WithStatus(status)` returns `this with { Status, UpdatedAt }`, following the `Environment.Update` pattern.
- `IDeploymentRepository.UpdateAsync`, implemented in `Orchitect.Persistence/Repositories/Engine/DeploymentRepository.cs` like `EnvironmentRepository.UpdateAsync`.
- `DeploymentQueue` work item:
  - Resolve the repository from `sp`.
  - Set `Deploying`, then `Deployed` on success.
  - Set `Failed` on an exception, written with `CancellationToken.None`.
  - Leave `Deploying` when the run is detached on shutdown.
- Check: `GET /deployments/{id}` goes Pending → Deploying → Deployed, and Failed for an unknown resource type.

## Phase 5 – M4: runner timeout → review
- Add `ExecutorOptions.Timeout` (default 1h) and pass it through `ExecutorContext`.
- `DockerExecutor`:
  - Link a timeout CTS with the caller's token for `WaitContainerAsync`.
  - On timeout alone: `StopContainerAsync` with `WaitBeforeKillSeconds = StopGracePeriod`, drain the logs, then throw `TimeoutException`. The container is then removed.
- Check: `Timeout=00:00:30` → the deployment is `Failed` and the container is gone.

## Phase 6 – M5: cancel race → review
- Remove the `started` flag and the detach catch filter.
- The `finally` calls `CleanupContainerAsync`, which runs `InspectContainerAsync`:
  - running and caller cancelled → detach, with the existing warning
  - otherwise → remove
- Check: cancel during start detaches a running container, and cancel after exit removes it. Use a unit test with a mocked `DockerClient` if feasible, otherwise test manually.

## Phase 7 – M6: backend config file → review
- `TerraformProjectBuilder` writes `backend.tfbackend` (HCL `key = "escaped"`) with a 0600 `UnixCreateMode`.
- `TerraformProjectBuilderResult.BackendConfig` → `string? BackendConfigFile`.
- `RunInitAsync` passes `-backend-config=<file>`.
- Tests: the file content, the placeholders and the mode.
- Check: a remote azurerm `init` works, including `use_azuread_auth = "true"`.

## Phase 8 – M7: secrets out of container env → review
- `ExecutorContext.Secrets` holds the connection string, the Key Vault token environment and `ExecutorOptions.Configuration`.
- `ToEnvironment()` no longer merges `Configuration`.
- `DeploymentQueue` fills `Secrets`.
- `DockerExecutor`:
  - After create and before start, build a tar in memory with `System.Formats.Tar`: `secrets.json`, uid/gid 1654, mode 0400.
  - Extract it with `ExtractArchiveToContainerAsync` to `/run/orchitect`.
- Runner `Program.cs`:
  - `AddJsonFile("/run/orchitect/secrets.json", optional: true)`.
  - Copy top-level string keys into the process environment for Terraform and Azure.
  - Delete the file.
- Dockerfile: `mkdir /run/orchitect`, owned by `$APP_UID`.
- Tests: the tar builder, and that `ToEnvironment` excludes `Configuration`.
- Check: `docker inspect` shows no connection string, token or `ARM_*`.

## Phase 9 – M8: split DI registrations → review
- Replace `AddEngineInfrastructureServices` with two methods:
  - `AddEngineProvisioningServices()` (shared, score, Helm, Terraform) for the Runner and the Playground
  - `AddEngineExecutionServices(IConfiguration)` (executor, Docker client, token provider, queue) for the API
- Update `Orchitect.Api/Program.cs`, `Orchitect.Runner/Program.cs` and `Orchitect.Playground/Program.cs`.
- Update `RunnerServicesTests` if needed.

## Phase 10 – M9: leftover container sweep → review
- Add a new `Executor/RunnerContainerSweepService` (`BackgroundService`), registered with the execution services. It runs at startup and every 10 minutes.
  - List containers with the label `orchitect.runner=true` (from E9).
  - Remove those in the `exited` or `created` state.
  - Warn about running ones older than `Timeout + StopGracePeriod`.
- Check: restarting the API removes exited runner containers.

## Phase 11 – M10: provider plugin cache → review
- Add `ExecutorOptions.PluginCacheVolume` (default `orchitect-terraform-plugin-cache`, null disables it).
- When it's set, mount the volume at `/var/cache/terraform-plugins` and set `TF_PLUGIN_CACHE_DIR`.
- Dockerfile: create the directory, owned by `$APP_UID`.
- `RUNNER_TODO.md`: note that the cache assumes serial runs (H3).
- Check: the second run reuses the cached providers.
