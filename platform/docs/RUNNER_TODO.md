# Runner – Outstanding Work

Items left over after the Runner review (`REVIEW_CODE.md`) and the first pass of fixes on 2026-09-22.

## Where things are

- The API queues a background job in `CreateDeploymentEndpoint`. The job calls `IRunner.ExecuteAsync`, and `DockerRunner` starts a container from the `orchitect-runner:<cloud>-<iac>` image.
- Inside the container, `Orchitect.Runner/Program.cs` loads the Application and Deployment from the DB by ID and calls `IEngineOrchestrator.StartAsync`.
- Only these cross the boundary: the IDs as container args, and env vars from `RunnerOptions:Configuration`. The Runner resolves every concrete implementation through its own DI root.
- Build the image from `platform/`:
  ```bash
  docker build -f src/Orchitect.Runner/Dockerfile \
    --build-arg ORCHITECT_CLOUD_PROVIDER=azure \
    --build-arg ORCHITECT_IAC_PROVIDER=terraform \
    -t orchitect-runner:azure-terraform .
  ```

## TODO

### 1. Runner configuration (needed before an end-to-end run)
- [ ] Set `RunnerOptions:Configuration:ConnectionStrings__orchitect` so it points at `host.docker.internal:41031`. The container reaches the host through the `host-gateway` mapping that `DockerRunner` adds.
- [ ] Set the cloud credentials (`ARM_CLIENT_ID`, `ARM_CLIENT_SECRET`, `ARM_TENANT_ID`, `ARM_SUBSCRIPTION_ID`, ...) under `RunnerOptions:Configuration`.
- [ ] Keep both in user-secrets or env vars, not `appsettings.json`. Right now `appsettings.json` only has `RunnerOptions:Image`.
- [ ] Run a real deployment through Aspire (`dotnet run --project src/Orchitect.AppHost`, then POST a deployment via Bruno). Check that the API logs show the runner output, and that `docker ps -a` has no leftover `orchitect-runner-*` containers.
- [ ] Run a failing deployment (bad commit or missing template). The container should exit non-zero, and `DockerRunner` should log the error and throw.

### 2. Terraform state is lost after every run (**blocker for real use**)
- `TerraformProjectBuilder` writes state to `/tmp/orchitect/terraform/state/<name>` and there is no backend, so the state is gone when the container is removed.
- As a result, destroy and redeploy can't find existing state.
- [ ] Render a remote backend (e.g. `azurerm`) in `TerraformRenderer`. Pass the backend config as runner env vars.
- [ ] Revisit the "remnant files" check in `TerraformProjectBuilder` once state is no longer local.

### 3. Deployment status is never updated
- [ ] Set `Deployment.Status` to `Deployed` or `Failed`. Either the Runner does it after `StartAsync`, or the API does it from the `DockerRunner` result. The API is the simpler option because it already knows the exit code.

### 4. Secrets baked into the API image
- [ ] Remove the `ARG`/`ENV ARM_*` block (including `ARM_CLIENT_SECRET`) from `src/Orchitect.Api/Dockerfile`. Credentials belong to the runner, and they should be passed at runtime.
- [ ] Remove Terraform, Azure CLI, Helm and `terraform-config-inspect` from the API image once Playground no longer needs them in-process.

### 5. Runner error reporting
- [ ] `TerraformDriver.ApplyAsync`/`DestroyAsync` ignore the terraform exit code and non-success plan states. A failed apply still exits 0. They should throw, or return a result the orchestrator turns into a failure.
- [ ] `--help` fails without a connection string, because `AddPersistenceServices` reads it eagerly. This is minor. Defer the read, or accept it.

### 6. Unused abstractions (decide: wire up or delete)
- [ ] `ISecretProvider` / `AzureKeyVaultSecretProvider`: not registered, not used. If needed, resolve it **inside the Runner**. Never pass it across the boundary.
- [ ] `IStorageLogProvider` / `FileSystemStorageProvider`: not registered, not implemented. `IStorageProvider.GetAsync<TOut>` returns `Task`, not `Task<TOut>`. Runner logs already reach the API through container stdout, so this may not be needed.
- [ ] Helm driver/validator/parser are registered, but there is no Helm `IProvisioner`.

### 7. Image (optional)
- [ ] The image is ~1.46 GB. `golang` stays in the final image only to build `terraform-config-inspect`. Build it in a separate Go stage and copy the binary across.
- [ ] Pin tool versions in `docker-configure.sh`. Right now `terraform-config-inspect@latest`, Helm's `get-helm-3` from `main`, and the Azure CLI installer (`curl | bash`) are all unpinned.
- [ ] Fill in the `gcp`, `opentofu` and `pulumi` branches in `docker-configure.sh`. They are stubs, so the build succeeds but no tools get installed.
- [ ] Image selection is a single `RunnerOptions:Image`. When more cloud/IaC combinations are needed, derive the tag from the environment/templates.
