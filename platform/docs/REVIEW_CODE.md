I want you to review the current Orchitect repository and reconstruct the architecture around the Runner system before suggesting any code changes.

Do NOT assume the architecture from this prompt is complete. The repository is the source of truth.

## Context

Orchitect is a .NET/C# platform orchestrator.

There are two important projects/concepts:

* `Orchitect.Infrastructure`

    * Contains infrastructure implementations.
    * Contains `DockerRunner`, which implements `IRunner`.
    * `DockerRunner` is responsible for starting an Orchitect Runner Docker container.
    * It should NOT itself perform the provisioning work.

* `Orchitect.Runner`

    * Is a separate executable/project.
    * Its compiled application is placed inside a Docker image.
    * The Docker container runs `Orchitect.Runner`.
    * `Orchitect.Runner` is responsible for resolving the concrete implementations it needs and actually executing the work.
    * It has its own composition root / dependency injection setup.

The important boundary is:

```text
Orchitect.Infrastructure
        │
        │ Docker API
        ▼
   Docker container
        │
        ▼
Orchitect.Runner
        │
        ▼
 concrete implementations
```

The .NET objects from the control-plane process cannot simply be passed into the container.

For example, the current abstractions include things like:

```csharp
public record RunnerContext
{
    public required ISecretProvider SecretProvider { get; init; }
    public required IProvisioner Provisioner { get; init; }
    public required IStorageLogProvider StorageLogProvider { get; init; }
}

public interface IRunner
{
    Task ExecuteAsync(
        RunnerContext context,
        CancellationToken cancellationToken = default);
}
```

However, this may not be the correct final design. Inspect the repository and determine what the existing architecture actually requires.

## Docker image

The Runner has its own Docker image.

The image is parameterised at build time using things such as:

```text
CLOUD_PROVIDER
IAC_PROVIDER
```

For example:

```text
azure + terraform
aws + terraform
azure + pulumi
```

There is one parameterised Dockerfile rather than separate Dockerfiles/images for every combination.

The Dockerfile invokes an `install-tools.sh` script at image-build time.

The script installs the required:

* OS dependencies
* cloud CLI
* IaC CLI
* Terraform config inspection tooling
* Helm
* other required execution tools

The script is a build-time concern. `DockerRunner` should NOT need access to it.

The resulting image is local to the machine running the Docker daemon for now.

## Current direction

The intended execution model is roughly:

```text
Orchitect
    │
    │ determines execution requirements
    ▼
IRunner
    │
    ▼
DockerRunner
    │
    │ docker run
    ▼
Orchitect.Runner container
    │
    │ resolves concrete implementations
    ▼
Provisioning execution
```

I want you to inspect the repository and work out the actual architecture rather than designing a new one from scratch.

## What I want from you

### 1. Map the existing architecture

Find and explain:

* `Orchitect.Runner`
* `Orchitect.Infrastructure`
* `IRunner`
* `RunnerContext`
* `DockerRunner`
* `IProvisioner`
* `ISecretProvider`
* `IStorageLogProvider`
* their implementations
* the existing DI/composition roots
* any other abstractions involved in runner execution

Show the dependency relationships.

### 2. Trace an execution

Trace the current code from the point where Orchitect decides to execute something through to the actual provisioning operation.

Show:

```text
caller
→ context creation
→ IRunner
→ DockerRunner
→ Docker container
→ Orchitect.Runner
→ DI resolution
→ provisioner
→ result/logging
```

Use actual classes and methods from the repository.

### 3. Identify the process boundary

Determine exactly what crosses from the Orchitect process into the Runner container.

Classify the existing `RunnerContext` properties as:

* can cross the Docker boundary directly
* must be serialised
* must be represented as environment/configuration
* must be represented as a secret
* must be recreated/resolved inside `Orchitect.Runner`
* should not cross the boundary at all

### 4. Review DockerRunner

Look at the current `DockerRunner` implementation and determine what it should actually be responsible for.

In particular consider:

* image selection
* local image existence
* container creation
* environment/configuration
* secrets
* volumes
* working directory
* networking
* stdout/stderr/logging
* cancellation
* exit codes
* cleanup
* Docker container lifecycle

Do not add functionality merely because it is possible. Keep the design minimal.

### 5. Review RunnerContext

Determine whether the current `RunnerContext` is architecturally correct.

If it currently contains interfaces such as:

```csharp
ISecretProvider
IProvisioner
IStorageLogProvider
```

determine whether those should actually be there given that `Orchitect.Runner` is a separate process.

Propose the smallest change required, if any.

### 6. Review the Runner composition root

Inspect how `Orchitect.Runner` currently registers and resolves its dependencies.

Determine:

* how it knows which concrete `IProvisioner` to use
* how it knows which concrete `ISecretProvider` to use
* how it knows which concrete `IStorageLogProvider` to use
* how configuration reaches it
* how cloud/IaC provider selection reaches it
* whether this is currently clean or has architectural problems

### 7. Review the Dockerfile and install-tools.sh

Inspect the actual Dockerfile and `install-tools.sh`.

Determine:

* which tools are installed at build time
* which configuration is build-time configuration
* which configuration should be runtime configuration
* whether anything is incorrectly baked into the image
* whether the Docker layering makes sense
* whether the Runner image contains everything `Orchitect.Runner` actually requires

Do not redesign the image system unless the repository reveals a real problem.

### 8. Give me the resulting architecture

After inspecting everything, give me:

1. Current architecture
2. Problems you found
3. Recommended architecture
4. Minimal changes required
5. Concrete class/interface relationships
6. A concrete execution flow

Use actual repository names wherever possible.

## Important constraints

Be critical and pragmatic.

Do not assume my proposed architecture is correct just because I described it above.

Do not introduce unnecessary abstractions.

Do not introduce a generic "Runner Framework", "Execution Engine", "Orchestration Context", etc. unless the existing code demonstrates a real need for it.

Do not turn `DockerRunner` into a second dependency injection/composition system.

The key architectural question is:

> Where is the process boundary, what information crosses it, and which side owns the concrete implementations?

Answer that from the actual repository.

Before proposing code, inspect the relevant projects, interfaces, implementations, DI registration, Dockerfile, and Runner entry point.
