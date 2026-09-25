# Runner – Outstanding Work

Items left over after the Runner review (`REVIEW_CODE.md`) and the first pass of fixes on 2026-09-22. A second pass on 2026-09-23 added remote Terraform state, Runner-side secrets, Runner-side destroy and error reporting.

## Where things are

- The API queues a background job in `CreateDeploymentEndpoint`. The job calls `IExecutor.ExecuteAsync`, and `DockerExecutor` starts a container from the `ExecutorOptions:Image` image (e.g. `orchitect-runner:terraform`).
- Inside the container, `Orchitect.Runner/Program.cs`:
  1. loads the Application and Deployment from the DB by ID
  2. loads the mapped secrets into its environment (`ISecretEnvironmentLoader`)
  3. calls `IEngineOrchestrator.StartAsync` or `DestroyAsync`, depending on `--operation`
- Only these cross the boundary: the IDs (and `--operation provision|destroy`) as container args, and env vars built by `ExecutorOptions.ToEnvironment()`. The Runner resolves every concrete implementation through its own DI root.
- Destroy is Runner-side only for now: run the image with `--operation destroy`. There is no API endpoint yet.

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

The API flattens the typed sections into container env (`TerraformBackend__*`, `SecretProvider__*`) and passes `Configuration` through as-is.

The API validates `TerraformBackend` and `SecretProvider` at startup (`ValidateOnStart`), so invalid config stops the API from booting instead of failing inside a container. `Configuration` is opaque and not validated, so a typo in a key (e.g. `AZURE_CLIENTID`) only shows up when the Runner runs.

**Never log, serialize or put `ExecutorOptions.Configuration` values in exception messages.** It holds credentials.

| Section | Purpose |
|---|---|
| `Image` | Required. The runner image to start. |
| `Network`, `DatabaseHost`, `DatabasePort` | Docker network for the container and how the runner reaches Postgres. The runner's connection string is derived from the API's own `ConnectionStrings:orchitect`, with the host/port rewritten. |
| `LogLevel` | The runner's default log level (default `Information`). `Debug` logs the rendered Terraform, which includes input values. |
| `MemoryBytes`, `NanoCpus`, `PidsLimit` | Container limits (defaults 2 GiB, 2 CPUs, 512 PIDs). Set to null to remove a limit. |
| `TerraformBackend` | Where state lives. `Mode` is `Local` (default, lost with the container) or `Remote`. For `Remote`, `Type` is any Terraform backend (`azurerm`, `s3`, `gcs`, ...) and `Config` is backend-specific, passed as-is to `terraform init -backend-config`. Orchitect only substitutes `{applicationId}`, `{environmentId}` and `{projectName}`. Setting `Type`/`Config` with `Mode = Local` is rejected. |
| `SecretProvider` | Where the Runner reads extra secrets from. `Type` is `Environment` (default) or `AzureKeyVault`, and each provider has its own typed section (`AzureKeyVault:VaultUri`). `Mappings` maps env var name to secret name. They are loaded into the Runner's env before any terraform command. `Environment` reads the secret from another env var, which lets one credential set both `AZURE_*` and `ARM_*` (e.g. `Mappings:ARM_CLIENT_SECRET = AZURE_CLIENT_SECRET`). With no mappings it does nothing. |
| `Configuration` | Opaque env vars, e.g. cloud credentials. |

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
- [ ] Set the user-secrets from the Azure example. The container reaches a host-local database through the `host-gateway` mapping that `DockerExecutor` adds.
- [ ] Run a real deployment through Aspire (`dotnet run --project src/Orchitect.AppHost`, then POST a deployment via Bruno). Check that:
  - the runner output (relayed into the API logs by `DockerExecutor`) shows `terraform init` succeeding against the backend, which checks **backend auth** on its own
  - `plan`/`apply` succeed, which checks **provider auth** separately
  - `docker ps -a` has no leftover `orchitect-runner-*` containers
- [ ] Run a failing deployment (bad commit, missing template or failing apply). The container should exit non-zero, and `DockerExecutor` should log the error and throw.
- [ ] Point a `SecretProvider:Mappings` entry at a missing secret. The Runner should exit non-zero with an error naming the secret.

### 2. Terraform state persistence
- [x] Render a partial `backend "<type>" {}` and pass `TerraformBackend:Config` via `terraform init -backend-config`. Works for any backend.
- [x] Remove the "remnant files" check. With a remote backend the working directory (`/tmp/orchitect/terraform/<applicationId>/<environmentId>`) is recreated. Without one it is kept, because it holds the local state.
- [x] Runner-side destroy (`--operation destroy`). It builds the same project and backend config as provision, so it uses the same state. Destroy never deletes the state itself.
- [ ] End-to-end check against real Azure:
  - deploy
  - redeploy: plan shows no changes, proving the state survived
  - destroy: resources gone, state blob still there, `terraform state list` empty
- [ ] Confirm the destroy command against real terraform. It now applies the saved destroy plan with `apply -auto-approve <plan>`, and so far only unit tests with a fake command line have exercised it.
- [ ] API `DELETE /deployments/{id}` that queues a `--operation destroy` run.

### 3. Deployment status is never updated
- [ ] Set `Deployment.Status` to `Deployed` or `Failed` (`Runner_Isolation_Fix_Plan.md`, M3). Either the Runner does it after `StartAsync`, or the API does it from the `DockerExecutor` result. The API is the simpler option because it already knows the exit code.

### 4. Secrets baked into the API image
- [x] Remove the `ARG`/`ENV ARM_*` block from `src/Orchitect.Api/Dockerfile`.
- [x] Remove Terraform, Azure CLI, Helm, Go and `terraform-config-inspect` from the API image. Playground runs from source, not from this image.

### 5. Runner error reporting
- [x] `TerraformDriver.ApplyAsync`/`DestroyAsync` throw on a failed plan state or a non-zero terraform exit code, so the runner exits non-zero. Plan failures now carry terraform's stderr.
- [x] `EngineOrchestrator` throws when the score file is missing, has no resources, or references a resource type with no template, so the run exits non-zero. A resource dropping out silently would otherwise be planned for destruction.
- [x] When the API shuts down mid-run, `DockerExecutor` leaves the running container alone instead of force-removing it. SIGKILL would stop terraform mid-apply. The container now runs with an init process (`Init = true`); forwarding SIGINT to terraform is tracked in `Runner_Isolation_Fix_Plan.md` (M2). The warning includes the container ID.
- [ ] Clean up containers left behind by a cancelled run, e.g. an API startup sweep that removes exited containers labelled `orchitect.runner=true`. This is manual right now (`Runner_Isolation_Fix_Plan.md`, M9).
- [ ] `--help` fails without a connection string, because `AddPersistenceServices` reads it eagerly. Invalid `SecretProvider` config also fails it, because `AddRunnerServices` validates at startup. This is minor. Defer the reads, or accept it.

### 6. Unused abstractions (decide: wire up or delete)
- [x] `ISecretProvider` / `AzureKeyVaultSecretProvider`: registered in the Runner only, through `AddRunnerServices`, and selected by `SecretProvider:Type`. Adding a provider means a new `SecretProviderType` value, its options section and a `case` in `AddRunnerServices`.
- [ ] `IStorageProvider` / `FileSystemStorageProvider` (deferred): the plan is a stream-based `IStorageProvider` with a Blob implementation for plan artifacts. It is not needed for state, because Terraform's backend owns that. Plan files can contain secrets, so decide on retention and access first. `GetAsync<TOut>` still returns `Task`, not `Task<TOut>`.
- [ ] Helm driver/validator/parser are registered, but there is no Helm `IProvisioner`.

### 7. Image (optional)
- [x] Build `terraform-config-inspect` in a separate Go stage and copy the binary across.
- [x] Pin tool versions (Terraform, Helm, `terraform-config-inspect`) and base images, with checksum and signature verification.
- [ ] Fill in the `iac-opentofu` and `iac-pulumi` stages in the Dockerfile. They are stubs that fail the build when selected.
- [ ] Image selection is a single `ExecutorOptions:Image`. When more cloud/IaC combinations are needed, derive the tag from the environment/templates.

### 8. Design issues (Hard, from `Runner_Isolation_Branch_Review.md`)
- [ ] **H1** The runner holds the API's full DB credentials while running untrusted Terraform. First step: a dedicated Postgres role with `SELECT` on the engine tables. Proper fix: the API passes a run manifest and the runner never touches the database.
- [ ] **H2** The Key Vault token covers every vault and secret the API's identity can read. Resolve only the mapped secrets in the API, use a runner-only vault, or give the runner its own least-privilege identity.
- [ ] **H3** The deployment queue is serial, blocking and in-memory. Needs a durable queue, bounded concurrency with one run per application/environment, and non-blocking enqueue.
- [ ] **H4** A containerised API can't reach Docker, and mounting the socket is root-equivalent. Options: rootless Docker/Podman, a remote Docker host, a small job-launcher service, or a platform-native `IExecutor` (Kubernetes Jobs, Container Apps Jobs, ECS).

Until H1 and H2 are fixed, don't point the runner at untrusted template repositories or score files.
