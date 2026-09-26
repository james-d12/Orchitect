# Runner Isolation Branch Review

Review of `feature/runner_isolation` against `master`, 2026-09-25.

Focus areas: the runner Dockerfile and how secrets reach the runner, Terraform error handling and output parsing (`TerraformCommandLine`, `TerraformDriver`, `TerraformValidator`), `DockerExecutor`, and high-level design issues.

Findings are grouped by how much effort they take to fix:

- **Easy**: a local change to one or two files that doesn't affect other components. These can go in now.
- **Medium**: touches several classes or interfaces, changes behaviour other code relies on, or needs a small design decision.
- **Hard**: a design change across the API, runner and infrastructure, touching lots of code.

Each finding also has a severity: 🔴 high, 🟡 medium, 🟢 low.

## Summary

| # | Finding | Effort | Severity |
|---|---|---|---|
| E1 | Unknown `terraform plan` exit codes treated as success | Easy | 🔴 |
| E2 | Resources silently skipped in the orchestrator, so Terraform plans to destroy them | Easy | 🔴 |
| E3 | `terraform-config-inspect` diagnostics discarded; JSON parsing unguarded | Easy | 🔴 |
| E4 | Runner log level hardcoded to Debug, leaking inputs into API logs | Easy | 🟡 |
| E5 | Plan/apply arguments built by string interpolation (paths with spaces break) | Easy | 🟡 |
| E6 | No `-lock-timeout`; `apply` missing `-input=false`; duplicate `RunDestroyAsync` | Easy | 🟢 |
| E7 | `PreValidationFailed` exception drops the per-template reasons | Easy | 🟢 |
| E8 | No container hardening (caps, no-new-privileges, limits, init) | Easy | 🟡 |
| E9 | Container name collides after a detached run | Easy | 🟡 |
| E10 | Connection string read from process env only; Npgsql aliases not handled | Easy | 🟡 |
| E11 | Log streaming noise and an unawaited relay task | Easy | 🟢 |
| E12 | Missing/empty score file exits 0 | Easy | 🟢 |
| E13 | Validator: case-insensitive input names, recursive `variables.tf` search | Easy | 🟢 |
| E14 | Stale names in validation messages and `RUNNER_TODO.md`; `Image` not validated | Easy | 🟢 |
| E15 | Dockerfile: floating base tags, unsigned checksums, stages that fail on the legacy builder | Easy | 🟢 |
| M1 | HCL injection through score-file values in `TerraformRenderer` | Medium | 🔴 |
| M2 | No cancellation or SIGINT forwarding to Terraform (state locks left held) | Medium | 🟡 |
| M3 | Deployment status never updated | Medium | 🟡 |
| M4 | No overall timeout on runner containers | Medium | 🟡 |
| M5 | Cancel race in `DockerExecutor` can kill Terraform mid-run or leak containers | Medium | 🟡 |
| M6 | Backend config (possibly secrets) passed as CLI args | Medium | 🟡 |
| M7 | Secrets passed as container env vars, readable via `docker inspect` | Medium | 🔴 |
| M8 | Runner registers API-only services (queue, Docker client, executor options) | Medium | 🟢 |
| M9 | No sweep for leftover `orchitect-runner-*` containers | Medium | 🟢 |
| M10 | No Terraform provider plugin cache | Medium | 🟢 |
| H1 | Runner holds the API's full DB credentials while running untrusted Terraform | Hard | 🔴 |
| H2 | Key Vault token covers all of Key Vault, not just the mapped secrets | Hard | 🔴 |
| H3 | Deployment queue is serial, blocking and in-memory | Hard | 🟡 |
| H4 | Containerised API can't reach Docker; socket access is root-equivalent | Hard | 🟡 |

**Suggested order before merge:** E1, E2, E3, E4 and the rest of the Easy list, then M1 and M3. H1 and H2 need a design decision and can be tracked in `RUNNER_TODO.md`. Until then, don't point the runner at untrusted template repos or score files.

---

## Easy

### E1. 🔴 Unknown plan exit codes treated as success
`Provisioner/Terraform/TerraformDriver.cs:96-111`

The switch handles only `1` (Errored) and `0` (NoChanges). Every other exit code falls through to `Success`, including `-1`, `137` (OOM-killed) and `143` (SIGTERM), so `apply` runs against a plan file that may be missing or partial.

**Fix:** add an explicit `case (int)TerraformPlanResultExitCode.ChangesNeeded` for `Success`, and make the default `PlanFailed`.

### E2. 🔴 Skipped resources are planned for destruction
`EngineOrchestrator.cs:120-133`

When a resource template is missing, or a resource has `Parameters == null`, the orchestrator logs at Information and uses `continue`. With a remote backend the Terraform project is regenerated on every run, so a resource that drops out of the list shows up in `plan` as **to be destroyed**. A renamed template would tear down live infrastructure on the next deploy.

Also, a null `Parameters` isn't an error when all of the module's variables are optional.

**Fix:** throw when a template can't be resolved. Treat a null `Parameters` as an empty dictionary and let `TerraformValidator` decide whether required inputs are missing.

### E3. 🔴 `terraform-config-inspect` diagnostics discarded
`Provisioner/Terraform/TerraformValidator.cs:163-174`

On HCL errors, `terraform-config-inspect --json` writes the diagnostics as JSON **to stdout** and exits with code 1 (confirmed in the tool's `main.go`). The validator logs only "Could not get json output for {Module}", and the result becomes "Could not parse module in …", so the real error is lost.

Also, `JsonSerializer.Deserialize` isn't guarded. A `JsonException` escapes `Parallel.ForEachAsync` and fails validation for every template, not just the broken one.

**Fix:**
- Include `StdOut` and `StdErr` in the log and in the `ModuleInspection` error, or add a `Diagnostics` property to `TerraformConfig` and format it.
- Catch `JsonException` and return `ModuleInvalid`.

### E4. 🟡 Runner log level hardcoded to Debug
`Executor/DockerExecutor.cs:58`

`"Logging__LogLevel__Default=Debug"` is always set. At Debug, `TerraformProjectBuilder` logs the rendered `main.tf`, which contains every input value, and `TerraformDriver` logs init output. The executor relays all of it into the API's logs and OTel.

**Fix:** add a `LogLevel` to `ExecutorOptions`, default it to `Information`, and include it in `ToEnvironment()`.

### E5. 🟡 Plan/apply arguments built by string interpolation
`Provisioner/Terraform/TerraformCommandLine.cs:44-66`

`RunInitAsync` uses the `ArgumentList` overload, but plan, apply and destroy interpolate `-out={planFileOutput}` and `{planFile}` into a single string. The paths come from `Path.GetTempPath()`, so they break wherever the temp path contains a space (common on Windows and macOS).

**Fix:** use `WithArguments(IEnumerable<string>)` for every command.

### E6. 🟢 Terraform CLI flags and a duplicate method
`Provisioner/Terraform/TerraformCommandLine.cs`

- There's no `-lock-timeout` on plan or apply. With a remote backend, two runs for the same application and environment fail immediately on the state lock. Add something like `-lock-timeout=5m`.
- `apply` has no `-input=false`. A saved plan doesn't prompt, but adding the flag keeps it consistent with the other commands.
- `RunDestroyAsync` is identical to `RunApplyAsync`. Applying a destroy plan is correct, but the duplicate method suggests a difference that doesn't exist. Remove it and call `RunApplyAsync` from `TerraformDriver.DestroyAsync`, or add a doc comment.

### E7. 🟢 `PreValidationFailed` drops the reasons
`Provisioner/Terraform/TerraformDriver.cs:54-60`

The per-template validation messages are logged, but the `TerraformPlanResult` carries only "Could not validate all inputs.", and that's the text the runner exits with.

**Fix:** join the invalid results' `Message` values into the plan result's message.

### E8. 🟡 No container hardening
`Executor/DockerExecutor.cs:61-65`

`HostConfig` sets only the network and extra hosts. These are cheap to add:

- `CapDrop = ["ALL"]`
- `SecurityOpt = ["no-new-privileges"]`
- memory, CPU and PID limits, from new `ExecutorOptions` values
- `Init = true`, so an init process is PID 1 and handles signals and zombie reaping. It also lays the groundwork for M2.
- optionally `ReadonlyRootfs = true` with a tmpfs at `/tmp`

The image already runs as non-root (`USER $APP_UID`), which is good.

### E9. 🟡 Container name collides after a detached run
`Executor/DockerExecutor.cs:43`

The container name is `orchitect-runner-{DeploymentId}`. A run cancelled during API shutdown is deliberately left running under that name, so a retry, or a later destroy of the same deployment, fails with 409 Conflict at create time.

**Fix:** add a per-run suffix (a short GUID or timestamp). Put the deployment ID in a container label so containers can still be found by deployment.

### E10. 🟡 Connection string handling
`Executor/DockerExecutor.cs:200-229`

- `Environment.GetEnvironmentVariable("ConnectionStrings__orchitect")` works only when the value is an env var. It fails when the value comes from `appsettings.json` or user-secrets. Inject `IConfiguration` and call `GetConnectionString("orchitect")`.
- `DbConnectionStringBuilder` doesn't understand Npgsql aliases. With `Server=localhost`, `TryGetValue("Host")` misses the host, and setting `Host` then leaves both `Server` and `Host` in the string. Use `NpgsqlConnectionStringBuilder`, or check the known aliases.

### E11. 🟢 Log streaming noise and an unawaited task
`Executor/DockerExecutor.cs:253-335`

- On normal cancellation, `Task.Delay(RawOutputFlushInterval, cancellationToken)` throws. `catch (Exception)` catches it and logs it as a Warning with a stack trace. Add `catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)` before the general catch.
- When a read is in flight at cancellation, the pending `read` task is abandoned and never observed.
- `WaitForOutputAsync` cancels on timeout but never awaits `logStreaming`, so the relay can keep logging after `ExecuteAsync` returns. Await it briefly after cancelling.
- `RemoveContainerAsync` logs `DockerContainerNotFoundException` at Error. That's expected after a manual cleanup, so Warning fits better.

### E12. 🟢 Missing or empty score file exits 0
`EngineOrchestrator.cs:94-106`

When the score file can't be parsed or has no resources, the orchestrator logs a warning and returns normally, so the runner exits 0 and the deployment looks like it succeeded. This is already an open question in `RUNNER_TODO.md` §5. Recommendation: throw, so the run fails.

### E13. 🟢 Validator input matching
`Provisioner/Terraform/TerraformValidator.cs:133-158, 176-194`

- Input names are matched against module variables case-insensitively, but HCL is case-sensitive. An input `Name` passes validation, then `terraform validate` fails with a less clear error. Use `StringComparison.Ordinal`.
- `IsValidModuleDirectory` uses `SearchOption.AllDirectories`, so a `variables.tf` under `examples/` satisfies the check even when the module root has none. Use `TopDirectoryOnly`. Arguably the check isn't needed at all: `terraform-config-inspect` already reports the variables and outputs, whatever file they're declared in.

### E14. 🟢 Stale names and missing validation
- `EngineInfrastructureExtensions.cs:69,71`: the validation messages say `RunnerOptions:`, but the section is now `ExecutorOptions`.
- `docs/RUNNER_TODO.md` still refers to `DockerRunner`, `RunnerOptions`, `docker-configure.sh`, `ORCHITECT_CLOUD_PROVIDER` and the `orchitect-runner:azure-terraform` tag, which this branch removed or renamed. Several items in its §7 (Image) are now done: the separate Go stage and the pinned tool versions.
- `ExecutorOptions.Image` is `required`, but the configuration binder ignores `required` and there's no `[Required]`. A missing image is caught only when Docker is called. Add `[Required]`, or a `.Validate(o => !string.IsNullOrWhiteSpace(o.Image))`.

### E15. 🟢 Dockerfile tidy-ups
`src/Orchitect.Runner/Dockerfile`

The Dockerfile is in good shape: multi-stage, pinned Terraform/Helm/config-inspect versions, checksum verification, non-root final user and IaC stage selection. Remaining items:

- `alpine:3` and `golang:1.27-alpine` are floating tags. Pin them to a minor version or a digest for reproducible builds.
- The checksums are downloaded from the same origin as the binaries, so they catch corruption but not a compromised origin. Verify HashiCorp's `terraform_<v>_SHA256SUMS.sig` with its published GPG key.
- The `iac-opentofu` and `iac-pulumi` stages `exit 1`. BuildKit skips unused stages, but the legacy builder (`DOCKER_BUILDKIT=0`) builds every stage and fails. Document that BuildKit is required, or turn the stubs into no-ops that fail only when selected.

---

## Medium

### M1. 🔴 HCL injection through score-file values
`Provisioner/Terraform/TerraformRenderer.cs` (`RenderMainTf`, `QuoteIfNeeded`)

String values are wrapped in `"…"` with no escaping of `"`, `\`, `${` or `%{`. Values that look like `[...]` are inserted raw. Score-file parameters come from the application's repository, so a crafted value can:

- close the `module` block and inject, for example, `resource "null_resource" { provisioner "local-exec" { command = "…" } }`
- or use `${file("/proc/self/environ")}` interpolation to read the runner's environment

Either way the value runs code inside the runner, which holds the DB credentials, the Key Vault token and the `ARM_*` credentials (see H1 and H2).

**Fix, in order of preference:**
1. Render module inputs as JSON (`main.tf.json`) or pass them through a `terraform.tfvars.json` file. JSON syntax doesn't treat values as code, so the issue goes away. This needs the renderer, the project builder and tests to change, and the variable types from `TerraformConfig` to produce typed values instead of guessing with `bool/int/double.TryParse`.
2. As a stopgap: escape `\`, `"`, `${` → `$${` and `%{` → `%%{` in string values, and parse list values instead of passing them through raw.

### M2. 🟡 No cancellation or SIGINT forwarding to Terraform
`TerraformCommandLine.cs`, `Shared/CommandLine/CommandLineBuilder.cs`, `TerraformDriver.cs`, `TerraformProvisioner.cs`

None of the `ITerraformCommandLine` methods take a `CancellationToken`, and `CommandLineBuilder` calls `WaitForExitAsync()` without one. `TerraformProvisioner` receives a token and ignores it.

When `docker stop` sends SIGTERM, the .NET host (PID 1) exits and Terraform is killed hard. On a remote backend that can leave the **state lock held**, and it can leave an apply half-done.

**Fix:**
- Pass the token from `Program.cs` down through the orchestrator, provisioner, driver and command line.
- On cancellation, send SIGINT to the Terraform process (Terraform shuts down gracefully on SIGINT) and wait for it to exit, with a grace period before killing it.
- Pair this with `Init = true` (E8) and a `StopTimeout` long enough for Terraform to finish shutting down.

### M3. 🟡 Deployment status is never updated
`Queue/DeploymentQueue.cs`, `Orchitect.Domain/Engine/Deployment/Deployment.cs`

Deployments stay `Pending` forever, and failures are visible only in logs. `CreateDeploymentEndpoint` returns `202 Accepted` with a `Location`, which implies the status can be polled. This is already `RUNNER_TODO.md` §3, but it should be treated as a blocker.

**Fix:** in the queue work item, set `Deploying` before execution and `Deployed` or `Failed` from the result of `ExecuteAsync`. That needs:
- a status-transition method on `Deployment` (`Status` is currently `init`-only)
- a repository update method
- resolving `IDeploymentRepository` from the scoped `sp` passed to the work item, not from the constructor

### M4. 🟡 No overall timeout on runner containers
`Executor/DockerExecutor.cs:114`

`WaitContainerAsync` waits forever, and the queue processes one item at a time (H3), so one hung `terraform apply` blocks every deployment.

**Fix:** add `ExecutorOptions.Timeout`. On timeout, stop the container with a grace period (so SIGINT reaches Terraform, see M2), mark the deployment `Failed`, and decide whether to remove or keep the container for debugging.

### M5. 🟡 Cancel race in `DockerExecutor`
`Executor/DockerExecutor.cs:94-166`

- **Cancel during `StartContainerAsync`:** if the daemon has already started the container, `started` is still `false`. The `finally` block then force-removes the container and kills Terraform mid-run, which the detach logic exists to prevent. Inspect the container's state before deciding to remove it, instead of relying on the `started` flag.
- **Cancel after exit, during `WaitForOutputAsync`:** the `OperationCanceledException` matches the detach filter, so an exited container is left behind. Detach only when the container is still running.

### M6. 🟡 Backend config passed as CLI args
`TerraformCommandLine.cs:33`, `TerraformProjectBuilder.cs:82-96`

`-backend-config=key=value` puts backend settings in the process arguments, where `ps` can see them. Some backends take secrets that way (azurerm `access_key` or `sas_token`, s3 keys).

**Fix:** have `TerraformProjectBuilder` write the values to a `backend.hcl` file in the working directory, with permissions restricted to the owner, and pass `-backend-config=backend.hcl`. This changes `TerraformProjectBuilderResult`, the command line and tests.

### M7. 🔴 Secrets passed as container env vars
`Executor/DockerExecutor.cs:54-60`, `Queue/DeploymentQueue.cs:41`

The container environment includes:
- the DB connection string
- the Key Vault access token
- in `Environment` mode, the raw `ARM_*` values from `Configuration`

The container config stores all of these. Anyone who can run `docker inspect` can read them for the container's lifetime, which is indefinite for detached containers.

**Fix:** write secrets to a per-run file and mount it into the container on tmpfs, readable only by `APP_UID`. The runner adds it as a configuration source (for example, `AddJsonFile("/run/orchitect/secrets.json")`) and the host deletes the file afterwards. Only non-sensitive config stays in env vars. This changes both the executor and the runner's configuration loading. Until then, document the exposure and make the cleanup sweep (M9) a priority.

### M8. 🟢 Runner registers API-only services
`EngineInfrastructureExtensions.cs:22-30`, `Orchitect.Runner/Program.cs:20-22`

The runner calls `AddEngineInfrastructureServices`, which also registers:
- `QueuedHostedService`
- the `DockerClient`
- `ExecutorOptions` with `ValidateOnStart`
- `KeyVaultRunnerTokenProvider(new DefaultAzureCredential())`

Nothing breaks today because the runner never calls `host.StartAsync()` and these singletons are created lazily. It's fragile coupling, though: starting the host would start a queue in the runner and fail validation on the missing `ExecutorOptions:Image`.

**Fix:** split the registrations into a provisioning set (score, Terraform, Helm, orchestrator), used by the runner, and an executor/queue set, used by the API.

### M9. 🟢 No sweep for leftover runner containers
Detached and failed runs leave `orchitect-runner-*` containers, along with their env secrets (M7). Add a hosted service or startup task in the API that removes exited containers carrying an Orchitect label (see E9), and logs, or optionally stops, containers that have run longer than the timeout. Already listed in `RUNNER_TODO.md` §5.

### M10. 🟢 No provider plugin cache
Every run downloads its providers again; azurerm alone is 100 MB+. Options:
- set `TF_PLUGIN_CACHE_DIR` to a named volume mounted by the executor
- or pre-seed common providers into the image with a `filesystem_mirror` in a CLI config file

---

## Hard

### H1. 🔴 Runner holds the API's full DB credentials
`Executor/DockerExecutor.cs:57`, `Orchitect.Runner/Program.cs`

The runner receives the API's own `ConnectionStrings__orchitect`. It then clones template repositories and runs `terraform plan/apply` on them. Any module can read the process environment through `data "external"`, `local-exec`, `file("/proc/self/environ")` or a malicious provider, which exposes:
- the whole database: every organisation, the ASP.NET Identity tables and the stored encrypted credentials
- the Key Vault token and the `ARM_*` credentials

Running Terraform in a container provides little isolation while the container holds the most privileged credential in the system.

**Options:**
- **Medium step:** create a dedicated Postgres role for the runner, with `SELECT` on only the engine tables it reads, and pass that connection string instead. This needs role provisioning in migrations or setup scripts, plus separate configuration.
- **Proper fix (hard):** the runner doesn't connect to the database at all.
  - The API resolves the application, deployment and resource templates and passes a run manifest into the container, as a mounted file or through a narrow authenticated callback API.
  - The runner reports status back the same way.
  - This changes `Program.cs`, the orchestrator's inputs, the executor and the persistence dependencies, and it makes M3 cleaner.

### H2. 🔴 Key Vault token covers all of Key Vault
`Secret/Azure/KeyVaultRunnerTokenProvider.cs:28`

The API requests a token for `https://vault.azure.net/.default` using its own `DefaultAzureCredential`, and hands it to the runner. The token works on **every vault and every secret the API's identity can read**, not just the `SecretProvider:Mappings` entries. If the JWT signing secret or the encryption key move into Key Vault, the runner can read those too, and through H1/M1 so can template code.

**Options:**
- **Medium:** the API resolves only the mapped secrets and passes the values to the runner (combine with M7 so they don't travel as env vars). The exposure is then limited to the secrets that run needs. This removes the need for `KeyVaultRunnerTokenProvider` and `AzureCredentialFactory`'s delegated token path.
- **Medium:** a dedicated vault that holds only runner secrets, with the API's access to it limited to Secrets User.
- **Hard:** a separate least-privilege runner identity, using workload identity federation or managed identity in Azure, so the API never mints tokens for the runner. This needs infrastructure changes and a different auth flow in each hosting environment.

### H3. 🟡 Deployment queue is serial, blocking and in-memory
`Queue/QueuedHostedService.cs`, `Queue/BackgroundTaskQueueProcessor.cs`, `Queue/DeploymentQueue.cs`

- `QueuedHostedService` processes one item at a time, so a 20-minute apply blocks every other deployment.
- The channel has capacity 5 in `Wait` mode, and `QueueBackgroundWorkItemAsync` takes no cancellation token. The sixth concurrent `POST /deployments` hangs the HTTP request until space frees up.
- The queue lives in memory, so an API restart loses queued work, and those deployments stay `Pending` forever (see M3).
- Two runs for the same application and environment can execute at once, which a local backend doesn't handle and a remote backend handles only through lock failures (E6).

**Fix:**
- a durable queue (a `deployment_runs` table polled with `SELECT … FOR UPDATE SKIP LOCKED`, or a message broker)
- bounded concurrency across runs, with one run at a time per application/environment
- non-blocking enqueue, with a `503` or a queued status when the queue is full

This touches the queue abstraction, the endpoint, persistence (a new entity and migration) and status handling.

### H4. 🟡 Containerised API can't reach Docker
`src/Orchitect.Api/Dockerfile`, `EngineInfrastructureExtensions.cs:74`

`DockerExecutor` uses `new DockerClientConfiguration().CreateClient()`, which connects to the local Docker socket. The API image doesn't mount the socket, and `appuser` isn't in the docker group, so the executor works only when the API runs on the host (Aspire dev).

Mounting `/var/run/docker.sock` makes the API root-equivalent on the host, which makes every API vulnerability a host compromise.

**Options:**
- rootless Docker or Podman
- a remote Docker host over TLS
- a separate small job-launcher service that holds the socket
- a platform-native executor (Kubernetes Jobs, Azure Container Apps Jobs, ECS tasks) behind `IExecutor`

`IExecutor` is already the right seam for this. The hard part is the hosting and infrastructure decision.
