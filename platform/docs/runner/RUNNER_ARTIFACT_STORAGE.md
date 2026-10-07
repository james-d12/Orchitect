---
title: "Runner – artifact and log storage"
status: active
workstream: runner
milestone: "Runner Isolation"
issues: [129]
superseded_by: null
last_reviewed: 2026-10-07
---

# Runner – Artifact and Log Storage

## Context
The runner produces files that are lost when its container exits:
- the Terraform plan file (`plan-*.tfplan`, written by `TerraformDriver.PlanAsync` under the project's plan directory)
- the stdout/stderr of each Terraform command (`CommandLineResult`). Before this change it only went to the console log.

The first `IStorageProvider<T>` had no real implementation, and `GetAsync<TOut>` returned `Task`. It was removed in `86df4b0`. This doc replaces it with a stream-based abstraction for anything the runner needs to keep.

**What is already stored elsewhere:**
- **The Orchitect run plan.** `DeploymentRunPlan` (Domain) keeps the resolved `RunPlan` (inputs and context) as JSON in Postgres, through `StoredRunPlan.Serialize` in `RunPlanner`. That is Orchitect's record of *what* to run. It is separate from the provisioner's own plan file and is not part of this design.
- **Terraform state.** It stays with `TerraformBackendOptions` (`TerraformBackend:Mode=Remote`). Native backends (azurerm, s3, …) handle locking, which a plain blob store does not.

## Design

### `Orchitect.Storage` (leaf project)
A shared library with no Orchitect references. `Engine.Execution` and `Engine.Dispatch` use it now, and the API can use it later to read logs and artifacts back.

- `StorageKey` is a relative path with `/` separators. It rejects empty input, empty, `.` or `..` segments, a leading or trailing `/`, `\` and control characters.
- `IStorageProvider` works on streams, keyed by `StorageKey`:
  ```csharp
  Task WriteAsync(StorageKey key, Stream content, CancellationToken ct = default);   // overwrites
  Task<Stream?> OpenReadAsync(StorageKey key, CancellationToken ct = default);      // null when missing
  Task<bool> DeleteAsync(StorageKey key, CancellationToken ct = default);
  IAsyncEnumerable<StorageKey> ListAsync(StorageKey prefix, CancellationToken ct = default);
  ```
- `StorageOptions` (`Storage` section) has `Type` (`None` | `FileSystem` | `AzureBlob`), one options section per provider, and `GetValidationError()`. It follows `SecretProviderOptions`.
- `AddStorage(configuration)` validates the options and throws on invalid config, then registers the provider for `Type`. `None` registers nothing.
- The options live in `Orchitect.Storage`, not `Engine.Contracts`, because Contracts must not reference Orchitect assemblies (`EngineLayeringTests`). Dispatch references `Orchitect.Storage` so it can forward them. So `AzureBlobStorageOptions` also holds the env keys for the Blob token.

**Implementations:**

| Provider | Options | Notes |
|---|---|---|
| `FileSystemStorageProvider` | `Storage:FileSystem:RootPath` (absolute) | Writes to `<root>/.tmp/` and then moves the file into place. Rejects any key that resolves outside the root. In a runner container, the root only outlives the run when `ExecutorOptions:StorageHostPath` bind-mounts a host directory over it. |
| `AzureBlobStorageProvider` | `Storage:AzureBlob:ContainerUri` (absolute, no SAS query) | Uses `BlobContainerClient`, and the container must already exist. Credentials work like Key Vault: Dispatch mints a `https://storage.azure.com/.default` token (`AzureBlobRunnerTokenProvider`) and passes it through `RunnerSecretsFile`. Without a token, the runner falls back to `DefaultAzureCredential`. |

S3 (`AWSSDK.S3`, which also covers MinIO) and an in-memory provider can be added behind the same interface later. Adding one means:
- a `StorageProviderType` value
- its options section and validation
- a `case` in `AddStorage`
- forwarding in `ExecutorOptions.ToEnvironment()`, plus a token provider if it needs one

### Run artifacts (`Engine.Execution`)
- `IRunArtifactStore` builds keys as `runs/{runId}/{kind}/{name}`. `kind` is `plan` or `log`, and `runId` is `ORCHITECT_RUN_ID`.
- `RunArtifactStore` swallows and logs every failure except cancellation.
- `NullRunArtifactStore` is the default, registered by `AddEngineProvisioningServices`, so the Playground keeps nothing. `AddRunnerServices` replaces it when `Storage:Type` is set.
- `TerraformDriver` saves:
  - `log/terraform-{init|validate|plan|apply|destroy}.log` (exit code, stdout, stderr) after each command, including failed ones
  - `plan/plan-<timestamp>.tfplan` and `plan/plan-<timestamp>.json` after a plan that succeeded or had no changes. The JSON comes from `terraform show -json`, which runs only when storage is enabled. A failed `show` is logged and the run carries on.

### Wiring
```
Dispatch: ExecutorOptions.Storage      ──ToEnvironment()──▶ Storage__* env               (non-secret)
          AzureBlobRunnerTokenProvider ──RunnerSecretsFile─▶ Storage__AzureBlob__AccessToken (AzureBlob only)
          ExecutorOptions.StorageHostPath ──ToBinds()──────▶ <host>:<RootPath> bind mount  (FileSystem only)
Runner:   AddRunnerServices → AddStorage(config) → IStorageProvider → RunArtifactStore → TerraformDriver
```

## Decisions
- **The runner writes directly to storage.** Going through the Orchitect API would mean new run endpoints and large uploads passing through the API. The runner already gets scoped credentials the same way for Key Vault. (#129)
- **Terraform state stays with the Terraform backend.** This abstraction covers artifacts and logs, not state. Native backends handle locking. (#129)
- **Storage is off by default** (`Storage:Type=None`). Plan files and `show -json` output can contain secrets, so nothing is written until an operator chooses where it goes. (#129)
- **Retention and access are left to the provider.** Use Blob lifecycle management policies and container RBAC, or directory permissions for FileSystem. There is no app-level encryption or expiry in v1. (#129)
- **Writes are best effort.** A failed upload logs a warning and does not fail the run. Artifacts are diagnostics, and the apply has already happened. The Blob token lasts about an hour, so on a run near the `Timeout` the last uploads can fail this way. (#129)
- **No SAS URLs.** `ContainerUri` is passed as plain container env, so a query string is rejected to keep a SAS out of `docker inspect`. (#129)
- **First implementations are FileSystem and Azure Blob.** S3 and in-memory come later. (#129)

## Testing
- `Orchitect.Storage.Unit.Tests` covers `StorageKey` validation, option validation, `AddStorage` and the FileSystem provider.
- `Orchitect.Storage.Integration.Tests` runs the Azure Blob provider against Azurite in Testcontainers (needs Docker). Azurite runs with `--skipApiVersionCheck`, because the SDK can be ahead of the newest Azurite.
- Execution and Dispatch unit tests cover:
  - `RunArtifactStore`
  - the artifacts `TerraformDriver` saves
  - the token provider
  - env forwarding, plus the contract test that binds the env back into the runner's options

## Outstanding
- [x] `Orchitect.Storage`, `IRunArtifactStore`, `TerraformDriver` uploads and the Dispatch wiring (#129)
