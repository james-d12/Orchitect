---
title: "Runner – outstanding work and operator guide"
status: active
workstream: runner
milestone: "Runner Isolation"
issues: [100, 104, 108, 111, 114, 125, 126, 127, 128, 129, 130, 131, 132]
superseded_by: null
last_reviewed: 2026-10-04
---

# Runner – Outstanding Work

Items left over after the Runner review (`../archive/REVIEW_CODE.md`) and the first pass of fixes on 2026-09-22. A second pass on 2026-09-23 added remote Terraform state, Runner-side secrets, Runner-side destroy and error reporting.

## Where things are

- The API queues a background job in `CreateDeploymentEndpoint`. The job calls `IExecutor.ExecuteAsync`, and `DockerExecutor` starts a container from the `ExecutorOptions:Image` image (e.g. `orchitect-runner:terraform`).
- Inside the container, `Orchitect.Runner/Program.cs` calls `IEngineOrchestrator.RunAsync`, which talks to the API only through `IRunnerApiClient` (`/internal/runs/{runId}`):
  1. fetches the run descriptor (`GET`): operation, repository URL, commit, application and environment IDs
  2. clones the repository at the commit and parses `score.yaml` (`IScoreDriver`)
  3. submits the score for a plan (`POST /plan`)
  4. loads the mapped secrets into its environment (`ISecretEnvironmentLoader`)
  5. provisions or destroys the planned resources, and reports the outcome (`POST /complete`). A failure at any step is reported as `Failed` and the runner exits non-zero
- Only these cross the boundary: `--run-id <id>` as the container's only argument, `ORCHITECT_RUN_ID`, `ORCHITECT_API_URL` and the non-secret env vars built by `ExecutorOptions.ToEnvironment()`, and a `/run/orchitect/secrets.json` copied into the container before it starts (run token, Key Vault token, `Configuration`, OTLP headers). There is no connection string: the runner has no `Orchitect.Persistence` reference and no route to the database. It loads that file into its environment and deletes it at startup. The Runner resolves every concrete implementation through its own DI root, built after the arguments are parsed. It registers only `AddEngineProvisioningServices()`, `AddRunnerServices()` and `AddRunnerApiClient()`; the queue, the Docker client and `ExecutorOptions` come from `AddEngineDispatchServices()`, which only the API calls.
- `DELETE /deployments/{id}` queues a `Destroy` run; the runner reads the operation from the run descriptor. Terraform state is per application/environment, so destroy tears down everything in it; only the latest deployment of an application to an environment can be destroyed, and only while it is `Deployed` or `Failed` (409 otherwise). The deployment moves to `Destroying`, then `Destroyed` or `Failed` (a failed destroy can be retried). The row is kept. `GET /deployments/{id}` returns the status.
- `POST /deployments/{id}/cancel` cancels the latest queued or running run. The run row decides who wins: `DeploymentRun.Version` is Postgres `xmin`, so a stale write throws `DeploymentRunConflictException`. The queue claims the run (Queued → Running) before it touches the deployment, and the endpoint cancels a queued run only if that write wins; otherwise it treats the run as running. A queued run is marked `Cancelled` and skipped. A running run gets `CancelRequestedAt`, then is stopped by the work item that owns it (`DockerExecutor` sends SIGTERM, waits `StopGracePeriod`, then force-removes the container), or, if an earlier API process started it, by `IExecutor.SignalStopAsync` (SIGTERM to the container with that run's label). In that case the sweep records `Cancelled` once the runner exits. `RunPlanner` refuses a run with `CancelRequestedAt` (409 `RunNotRunning`), so a cancel that lands before planning records no resources. Whenever the API records a cancel (queue or sweep), it also calls `IRunCompleter` with `Failed`, so instances aren't left `Provisioning`/`Removing` when the runner was killed before it could report. A runner that exits 0 before the stop lands stays `Succeeded`, with `CancelRequestedAt` showing the late request. A cancelled deployment can be destroyed.

## Build

Build the runner image from `platform/`:
```bash
docker build -f src/Orchitect.Runner/Dockerfile \
  --build-arg ORCHITECT_IAC_PROVIDER=terraform \
  -t orchitect-runner:terraform .
```
BuildKit is required (it is the default builder). The legacy builder builds every stage, and the unsupported `iac-opentofu`/`iac-pulumi` stages fail on purpose.

The image pins Terraform, Helm, `terraform-config-inspect` and its base images. Terraform's `SHA256SUMS` is verified against HashiCorp's PGP key (fingerprint pinned in the Dockerfile) before the checksum check.

## Runner configuration

All of it lives under `ExecutorOptions` in the API. Keep real values in user-secrets or env vars, never in `appsettings.json`. `appsettings.json` only sets safe defaults: `TerraformBackend:Mode = Local` and `SecretProvider:Type = Environment`.

The API flattens the typed sections into container env (`TerraformBackend__*`, `SecretProvider__*`). `Configuration`, the Key Vault token and the run token go in the secrets file instead, so `docker inspect` doesn't show them.

The API validates `TerraformBackend` and `SecretProvider` at startup (`ValidateOnStart`), so invalid config stops the API from booting instead of failing inside a container. `Configuration` is opaque and not validated, so a typo in a key (e.g. `AZURE_CLIENTID`) only shows up when the Runner runs.

**Never log, serialize or put `ExecutorOptions.Configuration` values in exception messages.** It holds credentials.

| Section | Purpose |
|---|---|
| `Image` | Required. The runner image to start. |
| `ApiBaseUrl` | Required. The API's URL, passed to the runner as `ORCHITECT_API_URL`. A loopback host is rewritten to `host.docker.internal`, like the OTLP endpoint. The API must listen on an address the container can reach: on Linux, `host-gateway` can't reach a listener bound to `127.0.0.1` only, so the AppHost binds the API to `0.0.0.0`. |
| `Network` | Docker network for the container. |
| `DatabaseHost`, `DatabasePort` | Unused since #105: the runner no longer gets a connection string. Removed in #107. |
| `LogLevel` | The runner's default log level (default `Information`). `Debug` logs the rendered Terraform, which includes input values. |
| `MemoryBytes`, `NanoCpus`, `PidsLimit` | Container limits (defaults 2 GiB, 2 CPUs, 512 PIDs). Set to null to remove a limit. |
| `TerraformBackend` | Where state lives. `Mode` is `Local` (default, lost with the container) or `Remote`. For `Remote`, `Type` is any Terraform backend (`azurerm`, `s3`, `gcs`, ...) and `Config` is backend-specific. It's written to an owner-only `backend.tfbackend` file and passed with `terraform init -backend-config=<file>`, so values never appear in process arguments. Orchitect only substitutes `{applicationId}`, `{environmentId}` and `{projectName}`. Setting `Type`/`Config` with `Mode = Local` is rejected. |
| `SecretProvider` | Where the Runner reads extra secrets from. `Type` is `Environment` (default) or `AzureKeyVault`, and each provider has its own typed section (`AzureKeyVault:VaultUri`). `Mappings` maps env var name to secret name. They are loaded into the Runner's env before any terraform command. `Environment` reads the secret from another env var, which lets one credential set both `AZURE_*` and `ARM_*` (e.g. `Mappings:ARM_CLIENT_SECRET = AZURE_CLIENT_SECRET`). With no mappings it does nothing. |
| `Configuration` | Opaque env vars, e.g. cloud credentials. Delivered through the secrets file, not container env. |

Example (Azure):
```bash
cd src/Orchitect.Api
dotnet user-secrets set "ExecutorOptions:TerraformBackend:Mode" "Remote"
dotnet user-secrets set "ExecutorOptions:TerraformBackend:Type" "azurerm"
dotnet user-secrets set "ExecutorOptions:TerraformBackend:Config:storage_account_name" "<account>"
dotnet user-secrets set "ExecutorOptions:TerraformBackend:Config:container_name" "tfstate"
dotnet user-secrets set "ExecutorOptions:TerraformBackend:Config:key" "{applicationId}/{environmentId}.tfstate"
dotnet user-secrets set "ExecutorOptions:TerraformBackend:Config:use_azuread_auth" "true"
dotnet user-secrets set "ExecutorOptions:SecretProvider:Type" "AzureKeyVault"
dotnet user-secrets set "ExecutorOptions:SecretProvider:AzureKeyVault:VaultUri" "https://<vault>.vault.azure.net/"
dotnet user-secrets set "ExecutorOptions:SecretProvider:Mappings:ARM_CLIENT_ID" "arm-client-id"
dotnet user-secrets set "ExecutorOptions:SecretProvider:Mappings:ARM_CLIENT_SECRET" "arm-client-secret"
dotnet user-secrets set "ExecutorOptions:SecretProvider:Mappings:ARM_TENANT_ID" "arm-tenant-id"
dotnet user-secrets set "ExecutorOptions:SecretProvider:Mappings:ARM_SUBSCRIPTION_ID" "arm-subscription-id"
dotnet user-secrets set "ExecutorOptions:Configuration:AZURE_CLIENT_ID" "<bootstrap sp>"
dotnet user-secrets set "ExecutorOptions:Configuration:AZURE_CLIENT_SECRET" "<bootstrap sp secret>"
dotnet user-secrets set "ExecutorOptions:Configuration:AZURE_TENANT_ID" "<tenant>"
```
You can also put the `ARM_*` values straight under `Configuration` and skip Key Vault. For S3, use `Mode = Remote`, `Type = s3` with `bucket`/`key`/`region` and `AWS_*` credentials.

**Bootstrap credential.** The Runner reaches Key Vault with its *own* Azure identity: `AZURE_CLIENT_ID/SECRET/TENANT` under `Configuration`, or a managed/workload identity when hosted in Azure. Only after that can `ARM_*` come from Key Vault:
```
Runner -> AZURE_* / managed identity -> Key Vault -> ARM_* (process env) -> Terraform provider + backend
```
The Azure SDK never reads `ARM_*`, and Terraform never reads `AZURE_*`.

**Backend auth is separate from provider auth.** `terraform init` authenticates to the backend before any provider runs.
- azurerm with `use_azuread_auth = true`: the ARM identity needs *Storage Blob Data Contributor* on the state account.
- The S3 backend may need `role_arn` etc. in its `Config`.
- The Key Vault bootstrap identity needs *Key Vault Secrets User*.

## TODO

### 1. End-to-end run
Setup is described in [Runner configuration](#runner-configuration). A real run needs `TerraformBackend:Mode = Remote`. With `Local`, the Runner logs a warning and the state is lost when the container is removed.
- [ ] Set the user-secrets from the Azure example. The container reaches a host-local database through the `host-gateway` mapping that `DockerExecutor` adds. (#125)
- [ ] Run a real deployment through Aspire (`dotnet run --project src/Orchitect.AppHost`, then POST a deployment via Bruno). Check that: (#125)
  - the runner output (relayed into the API logs by `DockerExecutor`) shows `terraform init` succeeding against the backend, which checks **backend auth** on its own
  - `plan`/`apply` succeed, which checks **provider auth** separately
  - `docker ps -a` has no leftover `orchitect-runner-*` containers
- [ ] Run a failing deployment (bad commit, missing template or failing apply). The container should exit non-zero, `DockerExecutor` should log the error and return the exit code, and `GET /deployments/{id}` should show `Failed`. (#126)
- [ ] Point a `SecretProvider:Mappings` entry at a missing secret. The Runner should exit non-zero with an error naming the secret. (#126)

### 2. Terraform state persistence
- [x] Render a partial `backend "<type>" {}` and pass `TerraformBackend:Config` via `terraform init -backend-config`. Works for any backend.
- [x] Backend settings go in a 0600 `backend.tfbackend` file instead of `-backend-config=key=value` arguments, so secrets such as `access_key` or `sas_token` aren't visible in `ps` (`../archive/Runner_Isolation_Fix_Plan.md`, M6).
- [x] Remove the "remnant files" check. With a remote backend the working directory (`/tmp/orchitect/terraform/<applicationId>/<environmentId>`) is recreated. Without one it is kept, because it holds the local state.
- [x] Runner-side destroy (`--operation destroy`). It builds the same project and backend config as provision, so it uses the same state. Destroy never deletes the state itself.
- [ ] End-to-end check against real Azure: (#127)
  - deploy
  - redeploy: plan shows no changes, proving the state survived
  - destroy: resources gone, state blob still there, `terraform state list` empty
- [ ] Confirm the destroy command against real terraform. It now applies the saved destroy plan with `apply -auto-approve <plan>`, and so far only unit tests with a fake command line have exercised it. (#127)
- [x] API `DELETE /deployments/{id}` that queues a `--operation Destroy` run, plus `GET /deployments/{id}` for the status.
- [x] The unique index on `Deployments (ApplicationId, EnvironmentId, CommitId, Status)` is gone, so a failed commit can be retried and the same commit destroyed twice. A partial unique index (`IX_Deployments_ActiveRun`) now allows only one `Pending`, `Deploying` or `Destroying` deployment per application/environment. `POST /deployments` returns 409 while one is active, and a race that gets past that check is caught from the index as `ActiveDeploymentExistsException`. A plain `(ApplicationId, EnvironmentId, CreatedAt)` index serves the latest-deployment query.

### 3. Deployment status
- [x] `Deployment.Start()` moves a pending deployment to `Deploying`. `Deployment.ProcessDeploymentStatus(exitCode, exception)` takes the raw run result and decides the status: `Deployed`, `Failed`, or unchanged when the run was cancelled. `DeploymentQueue` only passes that data through, and `IExecutor` returns the runner's exit code instead of throwing on a non-zero exit (`../archive/Runner_Isolation_Fix_Plan.md`, M3).

### 4. Secrets baked into the API image
- [x] Remove the `ARG`/`ENV ARM_*` block from `src/Orchitect.Api/Dockerfile`.
- [x] Remove Terraform, Azure CLI, Helm, Go and `terraform-config-inspect` from the API image. Playground runs from source, not from this image.

### 5. Runner error reporting
- [x] `TerraformDriver.ApplyAsync`/`DestroyAsync` throw on a failed plan state or a non-zero terraform exit code, so the runner exits non-zero. Plan failures now carry terraform's stderr.
- [x] `EngineOrchestrator` throws when the score file is missing, has no resources, or references a resource type with no template, so the run exits non-zero. A resource dropping out silently would otherwise be planned for destruction.
- [x] When the API shuts down mid-run, `DockerExecutor` leaves the running container alone instead of force-removing it. SIGKILL would stop terraform mid-apply. The container now runs with an init process (`Init = true`). On SIGTERM the runner cancels its token and sends terraform SIGINT, giving it 5 minutes to exit before killing it. `ExecutorOptions:StopGracePeriod` (default 6 min) sets the container's stop timeout. The warning includes the container ID.
- [x] On cancellation the executor inspects the container instead of trusting a `started` flag. It detaches only if the container is still running, and otherwise removes it. A run that exits just before shutdown keeps its exit code (`../archive/Runner_Isolation_Fix_Plan.md`, M5).
- [x] Runs have an overall `ExecutorOptions:Timeout` (default 1h). On timeout the executor sends SIGTERM, waits up to `StopGracePeriod` for Terraform to shut down, removes the container, and the deployment becomes `Failed` (`../archive/Runner_Isolation_Fix_Plan.md`, M4).
- [x] `RunnerContainerSweepService` in the API removes finished containers labelled `orchitect.runner=true` at startup and every 10 minutes, including the ones left running by a cancelled run once they exit. It only removes containers from an earlier API process or older than `Timeout + StopGracePeriod`, and it warns about running ones past that limit (`../archive/Runner_Isolation_Fix_Plan.md`, M9).
- [x] Deployments left active by an earlier API process are reconciled by `RunnerContainerSweepService` at startup and every 10 minutes, before it removes containers. `Pending` becomes `Failed`, because its queued work was lost. `Deploying`/`Destroying` takes the exit code of the runner container of its latest `DeploymentRun` (label `orchitect.run-id`) once that container has exited, stays active while it is still running, and becomes `Failed` when there is no container or it never exited cleanly. The run gets the same outcome. Only deployments last updated before the API started are touched, so runs this process owns are left alone. It assumes a single API instance (H3).
- [x] The Engine now uses `Docker.DotNet.Enhanced` 4.3 (the maintained fork, and the one Testcontainers uses) instead of `Docker.DotNet` 3.125, which clashed with it in the integration-test host because both ship an assembly named `Docker.DotNet`. The client is built with `DockerClientBuilder`, which resolves the endpoint like the `docker` CLI (`DOCKER_HOST`, then the current context). The integration-test host removes `RunnerContainerSweepService`, so a test run never touches runner containers on the developer's Docker daemon.
- [x] `--help` failed without a connection string, because `AddPersistenceServices` read it eagerly, and with invalid `SecretProvider` config, because `AddRunnerServices` validates at startup. Since #105 the runner registers no persistence and builds its host inside the command action, so `--help` and argument errors don't touch configuration. (#128)

### 6. Unused abstractions (decide: wire up or delete)
- [x] `ISecretProvider` / `AzureKeyVaultSecretProvider`: registered in the Runner only, through `AddRunnerServices`, and selected by `SecretProvider:Type`. Adding a provider means a new `SecretProviderType` value, its options section and a `case` in `AddRunnerServices`.
- [ ] `IStorageProvider` / `FileSystemStorageProvider` (deferred): the plan is a stream-based `IStorageProvider` with a Blob implementation for plan artifacts. It is not needed for state, because Terraform's backend owns that. Plan files can contain secrets, so decide on retention and access first. `GetAsync<TOut>` still returns `Task`, not `Task<TOut>`. (#129)
- [ ] Helm driver/validator/parser are registered, but there is no Helm `IProvisioner`. (#100)

### 7. Image (optional)
- [x] Build `terraform-config-inspect` in a separate Go stage and copy the binary across.
- [x] Pin tool versions (Terraform, Helm, `terraform-config-inspect`) and base images, with checksum and signature verification.
- [ ] Fill in the `iac-opentofu` and `iac-pulumi` stages in the Dockerfile. They are stubs that fail the build when selected. (#130, #131)
- [ ] Image selection is a single `ExecutorOptions:Image`. When more cloud/IaC combinations are needed, derive the tag from the environment/templates. (#132)

### 8. Design issues (Hard, from `Runner_Isolation_Branch_Review.md`)
- [x] **H1** The runner held the API's full DB credentials while running untrusted Terraform. Fixed: the runner calls an internal API with a per-run token and never touches the database. It has no `Orchitect.Persistence` reference and gets no connection string. See [API_RUNNER_SEPARATION.md](API_RUNNER_SEPARATION.md) for the design. (#110, #202, #203, #204, #106, #105)
- [ ] **H2** The Key Vault token covers every vault and secret the API's identity can read. Not covered by the API/runner separation, which keeps the current secret injection ([API_RUNNER_SEPARATION.md](API_RUNNER_SEPARATION.md) §9). Options: resolve only the mapped secrets in the API and pass them in the secrets file, use a runner-only vault, or give the runner its own least-privilege identity. (#108)
- [ ] **H3** The deployment queue is serial, blocking and in-memory. Needs a durable queue, bounded concurrency and non-blocking enqueue. One run per application/environment is already enforced by `IX_Deployments_ActiveRun`. Queued work is still lost on restart: `RunnerContainerSweepService` marks those deployments `Failed` instead of running them. (#110, #111)
- [ ] **H4** A containerised API can't reach Docker, and mounting the socket is root-equivalent. Options: rootless Docker/Podman, a remote Docker host, a small job-launcher service, or a platform-native `IExecutor` (Kubernetes Jobs, Container Apps Jobs, ECS). (#114)

Until H2 is fixed, don't point the runner at untrusted template repositories or score files.
