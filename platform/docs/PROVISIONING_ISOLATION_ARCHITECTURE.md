# Resource Provisioning Isolation Architecture

**Date:** 2025-11-26
**Status:** Proposal
**Author:** Architecture Review

## Executive Summary

This document proposes architectural approaches to isolate the resource provisioning process from the Conductor Engine API. Currently, Terraform, Helm, and other IaC tools run within the API container, causing bloat, security concerns, and scalability limitations. This proposal outlines four architectural options with detailed analysis to guide decision-making.

---

## Table of Contents

1. [Current Architecture](#current-architecture)
2. [Problems with Current Approach](#problems-with-current-approach)
3. [Architectural Options](#architectural-options)
   - [Option 1: Job-Based Worker Architecture](#option-1-job-based-worker-architecture)
   - [Option 2: Message Queue Architecture](#option-2-message-queue-architecture)
   - [Option 3: Kubernetes Jobs](#option-3-kubernetes-jobs)
   - [Option 4: Serverless Functions](#option-4-serverless-functions)
4. [Comparison Matrix](#comparison-matrix)
5. [Implementation Considerations](#implementation-considerations)
6. [Migration Strategy](#migration-strategy)
7. [Recommendation](#recommendation)

---

## Current Architecture

### Flow Diagram

```
┌──────────────────────────────────────────────────────────┐
│          API Container (Bloated)                         │
│                                                          │
│  ┌─────────────────┐                                    │
│  │  POST /deploy   │                                    │
│  └────────┬────────┘                                    │
│           │                                             │
│           ▼                                             │
│  ┌─────────────────────────────┐                       │
│  │ CreateDeploymentEndpoint    │                       │
│  │ - Creates Deployment record │                       │
│  │ - Queues background task    │                       │
│  └────────┬────────────────────┘                       │
│           │                                             │
│           ▼                                             │
│  ┌─────────────────────────────┐                       │
│  │ QueuedHostedService         │                       │
│  │ - 5 background workers      │                       │
│  │ - In-memory queue           │                       │
│  └────────┬────────────────────┘                       │
│           │                                             │
│           ▼                                             │
│  ┌─────────────────────────────┐                       │
│  │ ResourceProvisioner         │                       │
│  │ - Parses Score files        │                       │
│  │ - Resolves templates        │                       │
│  └────────┬────────────────────┘                       │
│           │                                             │
│           ▼                                             │
│  ┌─────────────────────────────┐                       │
│  │ ResourceFactory             │                       │
│  └────────┬────────────────────┘                       │
│           │                                             │
│           ▼                                             │
│  ┌─────────────────────────────┐                       │
│  │ TerraformDriver             │                       │
│  │ - Validates templates       │                       │
│  │ - Builds project            │                       │
│  │ - Runs init/plan/apply      │                       │
│  └────────┬────────────────────┘                       │
│           │                                             │
│           ▼                                             │
│  ┌─────────────────────────────┐                       │
│  │ TerraformCommandLine        │                       │
│  │ - Executes terraform CLI    │                       │
│  │ - Executes helm CLI          │                       │
│  │ - Executes az CLI            │                       │
│  └─────────────────────────────┘                       │
│                                                          │
│  Installed in Container:                                │
│  - Terraform                                            │
│  - Helm                                                 │
│  - Azure CLI                                            │
│  - Go (terraform-config-inspect)                        │
│  - Git                                                  │
│                                                          │
└──────────────────────────────────────────────────────────┘
```

### Key Files

- **API Dockerfile**: `src/Conductor.Engine.Api/Dockerfile` (lines 34-70 install IaC tools)
- **Deployment Endpoint**: `src/Conductor.Engine.Api/Endpoints/Deployment/CreateDeploymentEndpoint.cs`
- **Background Queue**: `src/Conductor.Engine.Api/Queue/QueuedHostedService.cs` (5 workers)
- **Provisioner**: `src/Conductor.Engine.Infrastructure/Resources/ResourceProvisioner.cs`
- **Terraform Driver**: `src/Conductor.Engine.Infrastructure/Terraform/TerraformDriver.cs`

---

## Problems with Current Approach

### 1. Container Bloat
- **Issue**: API container includes Terraform, Helm, Azure CLI, Go toolchain
- **Impact**:
  - Large image size (500MB+ extra)
  - Longer build and deployment times
  - Unnecessary attack surface

### 2. Limited Concurrency
- **Issue**: Fixed pool of 5 background workers (QueuedHostedService.cs)
- **Impact**:
  - Only 5 concurrent deployments possible
  - Queue backlogs during peak usage
  - Cannot scale provisioning independently from API

### 3. Security Concerns
- **Issue**: API process has access to cloud credentials and IaC tools
- **Impact**:
  - Broader attack surface if API is compromised
  - Violates principle of least privilege
  - Credentials stored in API container environment

### 4. Resource Contention
- **Issue**: Terraform processes compete for CPU/memory with API
- **Impact**:
  - Terraform-heavy operations can slow API responses
  - No resource limits on individual deployments
  - Potential for memory exhaustion

### 5. Lack of Isolation
- **Issue**: All deployments share the same process space
- **Impact**:
  - Terraform crashes can affect other deployments
  - Difficult to implement per-deployment timeouts
  - Cannot run different Terraform versions

### 6. Observability Gaps
- **Issue**: Background tasks lack structured logging and status tracking
- **Impact**:
  - No real-time log streaming
  - Difficult to debug failed deployments
  - No way to cancel in-progress deployments

### 7. State Management
- **Issue**: In-memory queue lost on API restart
- **Impact**:
  - Pending deployments lost during restarts
  - No durability guarantees
  - Cannot implement retry logic

---

## Architectural Options

### Option 1: Job-Based Worker Architecture

#### Overview

Separate worker containers poll the database for pending deployments and execute provisioning in isolated processes.

#### Architecture Diagram

```
┌─────────────────────────────┐
│   API Container             │
│   (Lightweight)             │
│                             │
│   - REST Endpoints          │
│   - Business Logic          │
│   - Auth/Authorization      │
│   - NO IaC Tools            │
│                             │
│   CreateDeploymentEndpoint  │
│   ├─ Validates request      │
│   ├─ Creates Deployment     │
│   │  Status: Pending        │
│   └─ Returns 202 Accepted   │
│                             │
└───────────┬─────────────────┘
            │
            │ Writes
            ▼
┌─────────────────────────────┐
│       PostgreSQL/SQLite     │
│                             │
│   Deployments Table         │
│   ├─ Id                     │
│   ├─ Status (enum)          │
│   ├─ WorkerId               │
│   ├─ Logs (text)            │
│   ├─ StartedAt              │
│   ├─ CompletedAt            │
│   └─ ErrorMessage           │
│                             │
└───────────┬─────────────────┘
            │
            │ Polls (WHERE Status = Pending)
            │ UPDATE ... SET Status = InProgress, WorkerId = ?
            │
    ┌───────┴────────┬─────────────────┬──────────────┐
    │                │                 │              │
    ▼                ▼                 ▼              ▼
┌────────────┐  ┌────────────┐  ┌────────────┐  ┌────────────┐
│  Worker 1  │  │  Worker 2  │  │  Worker 3  │  │  Worker N  │
│            │  │            │  │            │  │            │
│ Console    │  │ Console    │  │ Console    │  │ Console    │
│ App        │  │ App        │  │ App        │  │ App        │
│            │  │            │  │            │  │            │
│ - Polls DB │  │ - Polls DB │  │ - Polls DB │  │ - Polls DB │
│ - Claims   │  │ - Claims   │  │ - Claims   │  │ - Claims   │
│   deploy   │  │   deploy   │  │   deploy   │  │   deploy   │
│ - Runs     │  │ - Runs     │  │ - Runs     │  │ - Runs     │
│   provisio │  │   provisio │  │   provisio │  │   provisio │
│   ner      │  │   ner      │  │   ner      │  │   ner      │
│ - Updates  │  │ - Updates  │  │ - Updates  │  │ - Updates  │
│   status   │  │   status   │  │   status   │  │   status   │
│            │  │            │  │            │  │            │
│ Terraform  │  │ Terraform  │  │ Terraform  │  │ Terraform  │
│ Helm       │  │ Helm       │  │ Helm       │  │ Helm       │
│ Azure CLI  │  │ Azure CLI  │  │ Azure CLI  │  │ Azure CLI  │
│ Git        │  │ Git        │  │ Git        │  │ Git        │
└────────────┘  └────────────┘  └────────────┘  └────────────┘
```

#### Technical Implementation

**New Project: Conductor.Engine.Worker**

```csharp
// Program.cs
public class Program
{
    public static async Task Main(string[] args)
    {
        var host = Host.CreateDefaultBuilder(args)
            .ConfigureServices((context, services) =>
            {
                services.AddPersistenceServices(context.Configuration);
                services.AddInfrastructureServices();
                services.AddHostedService<ProvisioningWorker>();
            })
            .Build();

        await host.RunAsync();
    }
}

// ProvisioningWorker.cs
public class ProvisioningWorker : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ProvisioningWorker> _logger;
    private readonly string _workerId;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            using var scope = _serviceProvider.CreateScope();
            var deploymentRepo = scope.ServiceProvider.GetRequiredService<IDeploymentRepository>();

            // Atomic claim operation
            var deployment = await deploymentRepo.ClaimNextPendingDeploymentAsync(_workerId, stoppingToken);

            if (deployment is null)
            {
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
                continue;
            }

            try
            {
                await ExecuteDeploymentAsync(deployment, scope.ServiceProvider, stoppingToken);
            }
            catch (Exception ex)
            {
                await deploymentRepo.MarkAsFailedAsync(deployment.Id, ex.Message, stoppingToken);
            }
        }
    }

    private async Task ExecuteDeploymentAsync(Deployment deployment, IServiceProvider sp, CancellationToken ct)
    {
        var provisioner = sp.GetRequiredService<IResourceProvisioner>();
        var appRepo = sp.GetRequiredService<IApplicationRepository>();
        var deploymentRepo = sp.GetRequiredService<IDeploymentRepository>();

        var app = await appRepo.GetByIdAsync(deployment.ApplicationId, ct);

        // Stream logs to database
        using var logCapture = new LogCapture(logs =>
            deploymentRepo.AppendLogsAsync(deployment.Id, logs, ct));

        await provisioner.StartAsync(app, deployment, ct);

        await deploymentRepo.MarkAsSucceededAsync(deployment.Id, ct);
    }
}
```

**Database Changes**

```csharp
// Updated Deployment entity
public sealed record Deployment
{
    public required DeploymentId Id { get; init; }
    public required ApplicationId ApplicationId { get; init; }
    public required EnvironmentId EnvironmentId { get; init; }
    public required CommitId CommitId { get; init; }
    public required DeploymentStatus Status { get; init; }
    public string? WorkerId { get; init; }
    public string? Logs { get; init; }
    public string? ErrorMessage { get; init; }
    public DateTime? StartedAt { get; init; }
    public DateTime? CompletedAt { get; init; }
    public required DateTime CreatedAt { get; init; }
    public required DateTime UpdatedAt { get; init; }
}

// Expanded enum
public enum DeploymentStatus
{
    Pending,
    InProgress,
    Succeeded,
    Failed,
    Cancelled
}

// New repository methods
public interface IDeploymentRepository
{
    Task<Deployment?> ClaimNextPendingDeploymentAsync(string workerId, CancellationToken ct);
    Task AppendLogsAsync(DeploymentId id, string logs, CancellationToken ct);
    Task MarkAsSucceededAsync(DeploymentId id, CancellationToken ct);
    Task MarkAsFailedAsync(DeploymentId id, string errorMessage, CancellationToken ct);
    Task CancelAsync(DeploymentId id, CancellationToken ct);
}
```

**New Dockerfile: Conductor.Engine.Worker/Dockerfile**

```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /app
COPY ./src ./
COPY Directory.Build.props ./
WORKDIR Conductor.Engine.Worker
RUN dotnet publish -c Release -o /app/out

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
RUN apt-get update && apt-get install -y --no-install-recommends \
    curl wget gpg git golang unzip \
 && rm -rf /var/lib/apt/lists/*

# Install Terraform
RUN wget -O- https://apt.releases.hashicorp.com/gpg | gpg --dearmor -o /usr/share/keyrings/hashicorp-archive-keyring.gpg \
 && echo "deb [arch=$(dpkg --print-architecture) signed-by=/usr/share/keyrings/hashicorp-archive-keyring.gpg] https://apt.releases.hashicorp.com $(. /etc/os-release && echo $VERSION_CODENAME) main" \
    > /etc/apt/sources.list.d/hashicorp.list \
 && apt-get update && apt-get install -y terraform \
 && rm -rf /var/lib/apt/lists/*

# Install Azure CLI
RUN curl -sL https://aka.ms/InstallAzureCLIDeb | bash

# Install Helm
RUN curl -fsSL -o get_helm.sh https://raw.githubusercontent.com/helm/helm/main/scripts/get-helm-3 \
 && chmod 700 get_helm.sh && ./get_helm.sh && rm get_helm.sh

# Install terraform-config-inspect
RUN go install github.com/hashicorp/terraform-config-inspect@latest
ENV PATH="/root/go/bin:${PATH}"

COPY --from=build /app/out ./
ENTRYPOINT ["./Conductor.Engine.Worker"]
```

**Updated API Dockerfile** (simplified)

```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /app
COPY ./src ./
COPY Directory.Build.props ./
WORKDIR Conductor.Engine.Api
RUN dotnet publish -c Release -o /app/out

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
RUN useradd -m appuser
USER appuser
WORKDIR /app
COPY --from=build /app/out ./
ENTRYPOINT ["./Conductor.Engine.Api"]
```

#### Pros

✅ **Simple Architecture**: No additional infrastructure needed (no message queues, no K8s)
✅ **Horizontal Scaling**: Deploy N workers, scales linearly
✅ **Process Isolation**: Worker crashes don't affect API
✅ **Independent Deployment**: Update workers without touching API
✅ **Resource Limits**: Apply CPU/memory limits per worker container
✅ **Durable**: Jobs survive API restarts (stored in database)
✅ **Cloud-Agnostic**: Works on Docker, VMs, K8s, anywhere
✅ **Graceful Shutdown**: Workers finish current job before stopping
✅ **Easy Debugging**: Worker logs separate from API logs
✅ **Cost Effective**: Scale workers based on load, scale down to zero

#### Cons

❌ **Database Polling**: Adds load to database (mitigated with reasonable intervals)
❌ **Eventual Consistency**: Small delay between job creation and execution
❌ **Distributed Locking**: Need atomic claim operation to prevent duplicate work
❌ **Stale Workers**: Need health checks to detect dead workers
❌ **Retry Logic**: Manual implementation required (vs built-in with message queues)

#### Complexity Score: 3/10

---

### Option 2: Message Queue Architecture

#### Overview

Use a message queue (RabbitMQ, Azure Service Bus, AWS SQS) to distribute deployment jobs to workers.

#### Architecture Diagram

```
┌─────────────────────────────┐
│   API Container             │
│                             │
│   CreateDeploymentEndpoint  │
│   ├─ Creates Deployment     │
│   ├─ Status: Pending        │
│   └─ Publishes message      │
│       DeploymentCreated     │
└───────────┬─────────────────┘
            │
            │ Publishes
            ▼
┌─────────────────────────────┐
│   Message Queue             │
│   (RabbitMQ / Service Bus)  │
│                             │
│   deployments.pending       │
│   │                         │
│   ├─ DeploymentId: 123      │
│   ├─ DeploymentId: 456      │
│   └─ DeploymentId: 789      │
│                             │
└────────┬────────────────────┘
         │
         │ Consumes (competing consumers)
         │
    ┌────┴────┬─────────┬──────────┐
    │         │         │          │
    ▼         ▼         ▼          ▼
┌────────┐ ┌────────┐ ┌────────┐ ┌────────┐
│Worker 1│ │Worker 2│ │Worker 3│ │Worker N│
│        │ │        │ │        │ │        │
│ Reads  │ │ Reads  │ │ Reads  │ │ Reads  │
│ from   │ │ from   │ │ from   │ │ from   │
│ queue  │ │ queue  │ │ queue  │ │ queue  │
│        │ │        │ │        │ │        │
│ Loads  │ │ Loads  │ │ Loads  │ │ Loads  │
│ Deploy │ │ Deploy │ │ Deploy │ │ Deploy │
│ from DB│ │ from DB│ │ from DB│ │ from DB│
│        │ │        │ │        │ │        │
│ Runs   │ │ Runs   │ │ Runs   │ │ Runs   │
│ Provis │ │ Provis │ │ Provis │ │ Provis │
│ ioner  │ │ ioner  │ │ ioner  │ │ ioner  │
│        │ │        │ │        │ │        │
│ ACKs   │ │ ACKs   │ │ ACKs   │ │ ACKs   │
│ msg    │ │ msg    │ │ msg    │ │ msg    │
└────────┘ └────────┘ └────────┘ └────────┘
```

#### Technical Implementation

**Message Definition**

```csharp
public record DeploymentCreatedMessage(Guid DeploymentId, Guid ApplicationId, Guid EnvironmentId);
```

**Publisher (in API)**

```csharp
public sealed class CreateDeploymentEndpoint : IEndpoint
{
    private static async Task<Results<Accepted, BadRequest>> HandleAsync(
        CreateDeploymentRequest request,
        IDeploymentRepository repository,
        IMessagePublisher publisher,
        CancellationToken ct)
    {
        var deployment = Deployment.Create(request);
        await repository.CreateAsync(deployment, ct);

        // Publish message
        await publisher.PublishAsync(new DeploymentCreatedMessage(
            deployment.Id.Value,
            deployment.ApplicationId.Value,
            deployment.EnvironmentId.Value
        ));

        return TypedResults.Accepted();
    }
}
```

**Consumer (in Worker)**

```csharp
public class DeploymentConsumer : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await _messageConsumer.ConsumeAsync<DeploymentCreatedMessage>(
            "deployments.pending",
            async (message, ct) =>
            {
                using var scope = _serviceProvider.CreateScope();
                var repo = scope.ServiceProvider.GetRequiredService<IDeploymentRepository>();

                var deployment = await repo.GetByIdAsync(new DeploymentId(message.DeploymentId), ct);

                // Mark as in progress
                await repo.UpdateStatusAsync(deployment.Id, DeploymentStatus.InProgress, ct);

                // Execute provisioning
                await ExecuteDeploymentAsync(deployment, scope.ServiceProvider, ct);
            },
            stoppingToken
        );
    }
}
```

**Queue Configuration Examples**

*RabbitMQ:*
```csharp
services.AddRabbitMQ(config =>
{
    config.HostName = "rabbitmq";
    config.QueueName = "deployments.pending";
    config.PrefetchCount = 1; // One message per worker at a time
});
```

*Azure Service Bus:*
```csharp
services.AddAzureServiceBus(config =>
{
    config.ConnectionString = Configuration["AzureServiceBus:ConnectionString"];
    config.QueueName = "deployments-pending";
    config.MaxConcurrentCalls = 1;
});
```

#### Pros

✅ **Decoupled**: API and workers completely independent
✅ **Built-in Retry**: Message queues have retry/dead-letter mechanisms
✅ **Delivery Guarantees**: At-least-once delivery ensures no lost jobs
✅ **Load Balancing**: Queue automatically distributes work
✅ **Priority Queues**: Can implement priority deployments
✅ **Backpressure**: Queue absorbs traffic spikes
✅ **Mature Ecosystem**: Well-established patterns and libraries
✅ **No Polling**: Event-driven, push-based
✅ **Message TTL**: Can set expiration on old jobs

#### Cons

❌ **Infrastructure Dependency**: Requires message queue (RabbitMQ, Service Bus, SQS)
❌ **Operational Overhead**: Another service to monitor and maintain
❌ **Cost**: Cloud message queues have per-message costs
❌ **Complexity**: More moving parts than Option 1
❌ **Duplicate Processing**: Need idempotency handling (at-least-once delivery)
❌ **Visibility**: Messages in queue are opaque (need separate monitoring)
❌ **Local Development**: Need to run message queue locally

#### Complexity Score: 6/10

---

### Option 3: Kubernetes Jobs

#### Overview

Each deployment creates a Kubernetes Job that runs in an isolated pod. Job executes once and terminates.

#### Architecture Diagram

```
┌─────────────────────────────┐
│   API Pod                   │
│                             │
│   CreateDeploymentEndpoint  │
│   ├─ Creates Deployment     │
│   │  Status: Pending        │
│   └─ Creates K8s Job        │
│       via K8s API           │
└───────────┬─────────────────┘
            │
            │ kubectl apply -f job.yaml
            ▼
┌─────────────────────────────────────────────┐
│   Kubernetes Cluster                        │
│                                             │
│   ┌───────────────────────────────────┐    │
│   │ Job Controller                    │    │
│   │ - Watches for new Jobs            │    │
│   │ - Creates Pods                    │    │
│   │ - Retries on failure              │    │
│   └───────────┬───────────────────────┘    │
│               │                             │
│               │ Creates Pods                │
│               ▼                             │
│   ┌────────────────┐  ┌────────────────┐   │
│   │ Pod: deploy-123│  │ Pod: deploy-456│   │
│   │                │  │                │   │
│   │ Init Container:│  │ Init Container:│   │
│   │ - git clone    │  │ - git clone    │   │
│   │                │  │                │   │
│   │ Main Container:│  │ Main Container:│   │
│   │ - Load context │  │ - Load context │   │
│   │ - Run TF       │  │ - Run TF       │   │
│   │ - Update DB    │  │ - Update DB    │   │
│   │                │  │                │   │
│   │ Status:        │  │ Status:        │   │
│   │ Completed ✓    │  │ Running...     │   │
│   └────────────────┘  └────────────────┘   │
│                                             │
│   Pod terminates after completion           │
│   Logs retained in K8s for 1 hour           │
│                                             │
└─────────────────────────────────────────────┘
```

#### Technical Implementation

**Job Creation (in API)**

```csharp
public sealed class CreateDeploymentEndpoint : IEndpoint
{
    private static async Task<Results<Accepted, BadRequest>> HandleAsync(
        CreateDeploymentRequest request,
        IDeploymentRepository repository,
        IKubernetesJobService k8sJobService,
        CancellationToken ct)
    {
        var deployment = Deployment.Create(request);
        await repository.CreateAsync(deployment, ct);

        // Create K8s Job
        var jobSpec = new JobSpec
        {
            Name = $"deployment-{deployment.Id.Value}",
            Image = "conductor-worker:latest",
            Env = new Dictionary<string, string>
            {
                ["DEPLOYMENT_ID"] = deployment.Id.Value.ToString(),
                ["CONNECTION_STRING"] = _configuration["ConnectionStrings:Default"]
            },
            RestartPolicy = "OnFailure",
            BackoffLimit = 3
        };

        await k8sJobService.CreateJobAsync(jobSpec, ct);

        return TypedResults.Accepted();
    }
}
```

**Job Manifest Template**

```yaml
apiVersion: batch/v1
kind: Job
metadata:
  name: deployment-{deploymentId}
  labels:
    app: conductor-worker
    deployment-id: "{deploymentId}"
spec:
  backoffLimit: 3
  ttlSecondsAfterFinished: 3600
  template:
    spec:
      restartPolicy: OnFailure
      initContainers:
      - name: git-clone
        image: alpine/git
        command:
        - git
        - clone
        - $(REPO_URL)
        - /workspace
        volumeMounts:
        - name: workspace
          mountPath: /workspace
      containers:
      - name: provisioner
        image: conductor-worker:latest
        env:
        - name: DEPLOYMENT_ID
          value: "{deploymentId}"
        - name: CONNECTION_STRING
          valueFrom:
            secretKeyRef:
              name: conductor-db-secret
              key: connection-string
        resources:
          requests:
            memory: "512Mi"
            cpu: "500m"
          limits:
            memory: "2Gi"
            cpu: "2000m"
        volumeMounts:
        - name: workspace
          mountPath: /workspace
      volumes:
      - name: workspace
        emptyDir: {}
```

**Worker Entrypoint** (single-run)

```csharp
public class Program
{
    public static async Task Main(string[] args)
    {
        var deploymentId = Environment.GetEnvironmentVariable("DEPLOYMENT_ID");

        var host = Host.CreateDefaultBuilder(args)
            .ConfigureServices((context, services) =>
            {
                services.AddPersistenceServices(context.Configuration);
                services.AddInfrastructureServices();
            })
            .Build();

        using var scope = host.Services.CreateScope();

        var repo = scope.ServiceProvider.GetRequiredService<IDeploymentRepository>();
        var provisioner = scope.ServiceProvider.GetRequiredService<IResourceProvisioner>();
        var appRepo = scope.ServiceProvider.GetRequiredService<IApplicationRepository>();

        var deployment = await repo.GetByIdAsync(new DeploymentId(Guid.Parse(deploymentId)));
        var app = await appRepo.GetByIdAsync(deployment.ApplicationId);

        try
        {
            await repo.UpdateStatusAsync(deployment.Id, DeploymentStatus.InProgress);
            await provisioner.StartAsync(app, deployment, CancellationToken.None);
            await repo.MarkAsSucceededAsync(deployment.Id);
            Environment.Exit(0); // Success
        }
        catch (Exception ex)
        {
            await repo.MarkAsFailedAsync(deployment.Id, ex.Message);
            Environment.Exit(1); // Failure
        }
    }
}
```

#### Pros

✅ **True Isolation**: Each deployment in separate pod
✅ **Resource Limits**: K8s enforces CPU/memory limits
✅ **Auto-Scaling**: Horizontal Pod Autoscaler can scale workers
✅ **Built-in Retry**: Job backoffLimit handles retries
✅ **Log Management**: K8s collects logs automatically
✅ **Timeout Support**: activeDeadlineSeconds enforces timeouts
✅ **Namespace Isolation**: Can run in separate namespace
✅ **Security**: Pod Security Policies, Network Policies
✅ **No Shared State**: Completely ephemeral execution
✅ **Failure Detection**: K8s detects and restarts failed jobs

#### Cons

❌ **K8s Required**: Must run on Kubernetes
❌ **Cold Start**: Pod creation/scheduling overhead (5-15s)
❌ **Image Pulls**: Slow if image not cached on node
❌ **Complexity**: K8s knowledge required
❌ **Cost**: More pods = higher resource usage
❌ **Local Dev**: Need local K8s (kind, minikube)
❌ **Job Cleanup**: Need to clean up completed jobs
❌ **Credentials**: Must inject credentials into each pod
❌ **Networking**: Jobs need network access to DB

#### Complexity Score: 8/10

---

### Option 4: Serverless Functions

#### Overview

Trigger serverless functions (AWS Lambda, Azure Functions, Google Cloud Functions) for each deployment.

#### Architecture Diagram

```
┌─────────────────────────────┐
│   API (Cloud-hosted)        │
│                             │
│   CreateDeploymentEndpoint  │
│   ├─ Creates Deployment     │
│   ├─ Status: Pending        │
│   └─ Invokes Lambda         │
│       via SDK               │
└───────────┬─────────────────┘
            │
            │ Invoke(async)
            ▼
┌─────────────────────────────────────────────┐
│   AWS Lambda / Azure Functions              │
│                                             │
│   Function: DeploymentProvisioner           │
│   - Runtime: .NET 8 custom runtime          │
│   - Memory: 2048 MB                         │
│   - Timeout: 15 minutes                     │
│   - Concurrency: 100                        │
│                                             │
│   ┌────────────────────────────────────┐   │
│   │ Lambda Execution                   │   │
│   │                                    │   │
│   │ 1. Receives event with DeploymentId│   │
│   │ 2. Downloads Terraform to /tmp     │   │
│   │ 3. Connects to DB                  │   │
│   │ 4. Loads deployment context        │   │
│   │ 5. Runs TF in /tmp                 │   │
│   │ 6. Updates DB with results         │   │
│   │                                    │   │
│   │ Cold start: ~500ms                 │   │
│   │ Warm start: ~50ms                  │   │
│   └────────────────────────────────────┘   │
│                                             │
│   Auto-scaling: 0 to 1000 concurrent        │
│   Pay-per-invocation                        │
│                                             │
└─────────────────────────────────────────────┘
```

#### Technical Implementation

**Lambda Invocation (in API)**

```csharp
public sealed class CreateDeploymentEndpoint : IEndpoint
{
    private static async Task<Results<Accepted, BadRequest>> HandleAsync(
        CreateDeploymentRequest request,
        IDeploymentRepository repository,
        IAmazonLambda lambdaClient,
        CancellationToken ct)
    {
        var deployment = Deployment.Create(request);
        await repository.CreateAsync(deployment, ct);

        // Invoke Lambda asynchronously
        var invokeRequest = new InvokeRequest
        {
            FunctionName = "conductor-provisioner",
            InvocationType = InvocationType.Event, // Async
            Payload = JsonSerializer.Serialize(new
            {
                DeploymentId = deployment.Id.Value
            })
        };

        await lambdaClient.InvokeAsync(invokeRequest, ct);

        return TypedResults.Accepted();
    }
}
```

**Lambda Function**

```csharp
public class Function
{
    private readonly IServiceProvider _serviceProvider;

    public Function()
    {
        var services = new ServiceCollection();
        services.AddPersistenceServices();
        services.AddInfrastructureServices();
        _serviceProvider = services.BuildServiceProvider();
    }

    public async Task<string> FunctionHandler(DeploymentEvent evt, ILambdaContext context)
    {
        // Download Terraform to /tmp (Lambda has 512MB-10GB /tmp)
        await DownloadTerraformAsync("/tmp/terraform");

        using var scope = _serviceProvider.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<IDeploymentRepository>();
        var provisioner = scope.ServiceProvider.GetRequiredService<IResourceProvisioner>();

        var deployment = await repo.GetByIdAsync(new DeploymentId(evt.DeploymentId));

        try
        {
            await provisioner.StartAsync(deployment, CancellationToken.None);
            await repo.MarkAsSucceededAsync(deployment.Id);
            return "Success";
        }
        catch (Exception ex)
        {
            await repo.MarkAsFailedAsync(deployment.Id, ex.Message);
            throw;
        }
    }
}

public record DeploymentEvent(Guid DeploymentId);
```

**Lambda Configuration**

```yaml
# AWS SAM template.yaml
Resources:
  ProvisionerFunction:
    Type: AWS::Serverless::Function
    Properties:
      FunctionName: conductor-provisioner
      Runtime: provided.al2 # Custom .NET runtime
      Handler: Conductor.Engine.Worker::Conductor.Engine.Worker.Function::FunctionHandler
      MemorySize: 2048
      Timeout: 900 # 15 minutes (max)
      EphemeralStorage:
        Size: 10240 # 10 GB /tmp storage
      Environment:
        Variables:
          CONNECTION_STRING: !Ref DbConnectionString
      ReservedConcurrentExecutions: 100
```

#### Pros

✅ **Zero Infrastructure**: No servers to manage
✅ **Auto-Scaling**: Scales to 1000+ concurrent by default
✅ **Cost Efficient**: Pay only for execution time
✅ **High Availability**: Cloud provider manages HA
✅ **No Idle Cost**: Scale to zero when not in use
✅ **Global**: Can deploy to multiple regions
✅ **Monitoring**: Built-in CloudWatch/Application Insights

#### Cons

❌ **Execution Limits**: 15-minute timeout (AWS Lambda)
❌ **Cold Starts**: 500ms-2s startup time
❌ **Vendor Lock-in**: Tied to AWS/Azure/GCP
❌ **Limited Disk**: 512MB-10GB ephemeral storage
❌ **Binary Size**: Must fit in deployment package (250MB unzipped)
❌ **Terraform Install**: Must download on each cold start
❌ **Networking**: VPC configuration required for DB access
❌ **Debugging**: Harder to debug than containers
❌ **Local Testing**: Limited local emulation
❌ **Long Deployments**: May hit timeout on large IaC operations

#### Complexity Score: 7/10

---

## Comparison Matrix

| Criteria | Option 1: Job Workers | Option 2: Message Queue | Option 3: K8s Jobs | Option 4: Serverless |
|----------|----------------------|------------------------|-------------------|---------------------|
| **Complexity** | ⭐⭐⭐ Low | ⭐⭐⭐⭐⭐⭐ Medium | ⭐⭐⭐⭐⭐⭐⭐⭐ High | ⭐⭐⭐⭐⭐⭐⭐ Med-High |
| **Infrastructure** | Minimal (DB only) | Moderate (DB + Queue) | High (K8s cluster) | Minimal (Serverless) |
| **Scalability** | ⭐⭐⭐⭐ Good | ⭐⭐⭐⭐⭐ Excellent | ⭐⭐⭐⭐⭐ Excellent | ⭐⭐⭐⭐⭐ Excellent |
| **Cost** | 💰 Low | 💰💰 Medium | 💰💰💰 High | 💰 Pay-per-use |
| **Isolation** | Process-level | Process-level | Pod-level | Function-level |
| **Operational Overhead** | Low | Medium | High | Very Low |
| **Deployment Limits** | None | None | None | 15 min timeout |
| **Local Development** | ⭐⭐⭐⭐⭐ Excellent | ⭐⭐⭐ Good | ⭐⭐ Poor | ⭐⭐ Poor |
| **Retry Mechanism** | Manual | Built-in | Built-in | Built-in |
| **Observability** | ⭐⭐⭐ Good | ⭐⭐⭐⭐ Very Good | ⭐⭐⭐⭐⭐ Excellent | ⭐⭐⭐⭐ Very Good |
| **Cloud Agnostic** | ✅ Yes | ✅ Yes | ✅ Yes | ❌ No |
| **Resource Limits** | Container-level | Container-level | Pod-level | Function-level |
| **Time to Implement** | 1-2 weeks | 2-3 weeks | 3-4 weeks | 2-3 weeks |
| **Cold Start Penalty** | None | None | 5-15s | 500ms-2s |
| **Debugging Ease** | ⭐⭐⭐⭐⭐ Excellent | ⭐⭐⭐⭐ Good | ⭐⭐⭐ Moderate | ⭐⭐ Difficult |
| **Suitable For** | Most use cases | High-volume | K8s-native | Variable load |

---

## Implementation Considerations

### Cross-Cutting Concerns

Regardless of option chosen, these concerns must be addressed:

#### 1. Deployment Lifecycle Management

**Status Tracking**
```csharp
public enum DeploymentStatus
{
    Pending,      // Created, waiting to be picked up
    InProgress,   // Worker is executing
    Succeeded,    // Provisioning completed successfully
    Failed,       // Provisioning failed
    Cancelled,    // User cancelled during execution
    TimedOut      // Exceeded maximum execution time
}
```

**Timestamps**
- `CreatedAt`: When deployment was requested
- `StartedAt`: When worker started execution
- `CompletedAt`: When worker finished (success or failure)
- Use for: SLA tracking, timeout detection, metrics

#### 2. Log Streaming

**Requirements**
- Real-time log streaming to API clients (Server-Sent Events)
- Persistent log storage for audit/debugging
- Structured logging for filtering

**Implementation Approaches**

*Option A: Database Log Append*
```csharp
await deploymentRepo.AppendLogsAsync(deploymentId, logEntry);
```
- Simple, works with all options
- Can cause DB write contention under load

*Option B: Separate Log Store*
```csharp
await logStore.AppendAsync(deploymentId, logEntry); // S3, Azure Blob, etc.
```
- Offloads DB
- API streams from log store

*Option C: Real-time Stream*
```csharp
await signalRHub.SendAsync("deployment-logs", deploymentId, logEntry);
```
- True real-time (< 100ms latency)
- Requires SignalR/WebSocket infrastructure

#### 3. Cancellation

**Challenge**: Worker may be mid-Terraform-apply when cancel requested

**Implementation**
```csharp
// API endpoint
POST /deployments/{id}/cancel
→ Sets Status = Cancelled
→ Stores cancellation token

// Worker checks periodically
if (await IsCancellationRequested(deploymentId))
{
    await terraform.DestroyAsync(); // Rollback
    throw new OperationCanceledException();
}
```

#### 4. Dead Worker Detection

**Problem**: Worker crashes mid-deployment, leaving status as `InProgress`

**Solution**: Heartbeat or timeout-based detection

```csharp
// Heartbeat approach (Option 1, 2)
await deploymentRepo.UpdateHeartbeatAsync(deploymentId, workerId);

// In separate cleanup job
var staleDeployments = await deploymentRepo.GetDeploymentsWithStaleHeartbeatAsync(TimeSpan.FromMinutes(10));
foreach (var d in staleDeployments)
{
    await deploymentRepo.MarkAsFailedAsync(d.Id, "Worker timeout");
}
```

```csharp
// Timeout approach (Option 3, 4)
if (deployment.Status == InProgress &&
    DateTime.UtcNow - deployment.StartedAt > TimeSpan.FromMinutes(30))
{
    await deploymentRepo.MarkAsTimedOutAsync(deployment.Id);
}
```

#### 5. Terraform State Management

**Current**: State stored in local filesystem within worker

**Considerations**
- Ephemeral workers need remote state backend (S3, Azure Blob, Terraform Cloud)
- State locking to prevent concurrent modifications
- State encryption for sensitive data

**Recommended**
```hcl
terraform {
  backend "azurerm" {
    storage_account_name = "conductorstates"
    container_name       = "tfstate"
    key                  = "${deployment_id}.tfstate"
  }
}
```

#### 6. Credentials Management

**Current**: ARM credentials in environment variables

**Recommendations**
- **Option 1/2**: Mount secrets as volumes or env vars in worker containers
- **Option 3**: K8s Secrets injected into Job pods
- **Option 4**: Lambda execution role with assume-role for cloud credentials

**Best Practice**: Use managed identities (Azure MSI, AWS IAM roles) instead of static credentials

#### 7. Monitoring & Alerting

**Metrics to Track**
- Deployment throughput (deployments/hour)
- Average deployment duration
- Success/failure rate
- Worker queue depth (Option 1/2)
- Worker pod count (Option 3)
- Function invocations (Option 4)

**Alerting**
- High failure rate (> 10%)
- Long-running deployments (> 30 min)
- Queue backlog (> 50 pending, Option 1/2)
- Dead workers detected
- Database connection failures

---

## Migration Strategy

### Phase 1: Preparation (No Downtime)

1. **Update Domain Model**
   - Add new fields to `Deployment` entity
   - Create database migration
   - Deploy migration (backward compatible)

2. **Add New API Endpoints**
   - `GET /deployments/{id}` - Get deployment status
   - `GET /deployments/{id}/logs` - Get logs
   - `POST /deployments/{id}/cancel` - Cancel deployment
   - Deploy API changes (old flow still works)

3. **Create Worker Project**
   - New `Conductor.Engine.Worker` project
   - Copy provisioning logic from API
   - Test locally

### Phase 2: Parallel Run (Controlled Rollout)

4. **Deploy Workers** (Not Yet Active)
   - Deploy worker containers (don't process yet)
   - Configure connection strings
   - Verify workers can connect to DB

5. **Feature Flag**
   - Add feature flag: `UseIsolatedWorkers`
   - When `false`: Use old QueuedHostedService
   - When `true`: Create deployment in `Pending` state for workers

6. **Gradual Rollout**
   - Enable for 10% of deployments
   - Monitor metrics (success rate, duration)
   - Ramp to 50%, then 100%

### Phase 3: Cutover

7. **Full Cutover**
   - Set feature flag to 100%
   - Monitor for 24-48 hours
   - Address any issues

8. **Cleanup**
   - Remove `QueuedHostedService` from API
   - Remove IaC tools from API Dockerfile
   - Remove feature flag code
   - Rebuild and deploy lightweight API

### Rollback Plan

At any point during Phase 2:
- Set feature flag to `false`
- All new deployments use old in-process flow
- Workers can continue finishing in-progress jobs

---

## Recommendation

### For Most Teams: **Option 1 - Job-Based Worker Architecture**

**Rationale:**

1. **Lowest Complexity**: No additional infrastructure beyond database. Easy to understand and debug.

2. **Cloud Agnostic**: Works on Docker, K8s, VMs, or any container runtime. No vendor lock-in.

3. **Fast Implementation**: Can be built in 1-2 weeks with existing codebase patterns.

4. **Cost Effective**: Only pay for worker compute. Can scale down to zero workers during off-hours.

5. **Excellent Debuggability**: Workers are just console apps. Can run locally, attach debugger, tail logs.

6. **Horizontal Scaling**: Add more workers as load increases. Linear scaling characteristics.

7. **Solves Core Problems**:
   - ✅ Removes IaC bloat from API
   - ✅ Enables independent scaling
   - ✅ Provides process isolation
   - ✅ Allows resource limits per worker
   - ✅ Durable across API restarts

### When to Consider Alternatives

**Choose Option 2 (Message Queue) if:**
- You already have message queue infrastructure (RabbitMQ, Service Bus)
- You need sub-second dispatch latency
- You want built-in retry with dead-letter queues
- You're building a high-volume system (> 1000 deployments/hour)

**Choose Option 3 (K8s Jobs) if:**
- You're already on Kubernetes
- You need strong resource isolation (separate pods)
- You want K8s-native features (RBAC, Network Policies)
- You can tolerate 5-15s cold start per deployment

**Choose Option 4 (Serverless) if:**
- You're cloud-native (AWS/Azure/GCP)
- You have highly variable load (0-100 deployments/min)
- All deployments complete in < 15 minutes
- You want zero operational overhead

---

## Next Steps

1. **Decision**: Review this document and select an option
2. **Spike**: Build proof-of-concept of selected option (3-5 days)
3. **Design Review**: Present POC to team, validate assumptions
4. **Implementation**: Follow migration strategy (1-3 weeks)
5. **Testing**: Load test with production-like workloads
6. **Rollout**: Gradual rollout with feature flag

---

## Appendix

### Estimated Effort

| Phase | Option 1 | Option 2 | Option 3 | Option 4 |
|-------|----------|----------|----------|----------|
| POC | 2 days | 3 days | 4 days | 3 days |
| Implementation | 1 week | 2 weeks | 3 weeks | 2 weeks |
| Testing | 2 days | 3 days | 4 days | 3 days |
| Deployment | 1 day | 2 days | 3 days | 2 days |
| **Total** | **~2 weeks** | **~3 weeks** | **~4 weeks** | **~3 weeks** |

### References

- Current API Dockerfile: `src/Conductor.Engine.Api/Dockerfile`
- Current Queue Service: `src/Conductor.Engine.Api/Queue/QueuedHostedService.cs`
- Current Provisioner: `src/Conductor.Engine.Infrastructure/Resources/ResourceProvisioner.cs`
- Terraform Driver: `src/Conductor.Engine.Infrastructure/Terraform/TerraformDriver.cs`
- CLAUDE.md: Project architecture guidelines

---

**Questions? Concerns? Alternative approaches?** Please discuss before proceeding with implementation.
