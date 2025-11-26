# Infrastructure State Management & Shared Resources

**Date:** 2025-11-26
**Status:** Proposal
**Context:** Assumes Option 1 (Job-Based Worker Architecture) from PROVISIONING_ISOLATION_ARCHITECTURE.md

## Executive Summary

This document proposes approaches for storing and managing infrastructure state in Conductor Engine to support flexible resource provisioning scenarios, including shared resources across multiple applications. The primary challenge is enabling both dedicated and shared resource models while maintaining clean separation of concerns and accurate state tracking.

---

## Table of Contents

1. [Problem Statement](#problem-statement)
2. [Current State Analysis](#current-state-analysis)
3. [Key Requirements](#key-requirements)
4. [Architectural Approaches](#architectural-approaches)
   - [Option 1: Resource Matching by Criteria](#option-1-resource-matching-by-criteria)
   - [Option 2: Explicit Resource References](#option-2-explicit-resource-references)
   - [Option 3: Resource Pools](#option-3-resource-pools)
   - [Option 4: Declarative Resource Graph](#option-4-declarative-resource-graph)
5. [State Storage Strategies](#state-storage-strategies)
6. [Comparison Matrix](#comparison-matrix)
7. [Recommendation](#recommendation)
8. [Implementation Guide](#implementation-guide)

---

## Problem Statement

### The Challenge

**Day 1 Scenario:**
```
Application A (API) → Requires Cosmos DB → Provision NEW Cosmos DB instance
```

**Day 7 Scenario:**
```
Application B (Worker) → Requires Cosmos DB → Use EXISTING Cosmos DB from Day 1
```

**Key Questions:**
1. How does the system know when to create vs. reuse a resource?
2. How do we store infrastructure state (Terraform outputs, connection strings, ARNs)?
3. How do we track which applications are using which resources?
4. How do we handle resource dependencies (DB depends on Resource Group)?
5. How do we support both dedicated and shared resource models?
6. How do we clean up resources when no longer needed?

### Real-World Examples

**Example 1: Shared Database**
- API service and background worker share the same Cosmos DB
- Both need connection strings
- DB should only be deleted when both apps are deleted

**Example 2: Dedicated Storage**
- Each API gets its own Blob Storage account
- No sharing between applications
- Deleted when application is deleted

**Example 3: Shared Infrastructure**
- Multiple apps in same environment share:
  - Resource Group (Azure)
  - VPC (AWS)
  - Kubernetes Cluster
  - Application Insights instance

**Example 4: Resource Dependencies**
```
Application A needs:
  → API Management Service
    → Requires Application Insights
      → Requires Resource Group
```

---

## Current State Analysis

### Existing Domain Models

#### ResourceTemplate
```csharp
public sealed record ResourceTemplate
{
    public ResourceTemplateId Id { get; init; }
    public string Name { get; init; }        // "Azure Cosmos DB"
    public string Type { get; init; }        // "azure-cosmosdb"
    public ResourceTemplateProvider Provider { get; init; }  // Terraform, Helm, etc.
    public IReadOnlyList<ResourceTemplateVersion> Versions { get; }
}
```

#### ResourceTemplateVersion
```csharp
public sealed record ResourceTemplateVersion
{
    public ResourceTemplateVersionId Id { get; init; }
    public ResourceTemplateId TemplateId { get; init; }
    public string Version { get; init; }     // "v1.0.0"
    public ResourceTemplateVersionSource Source { get; init; }  // Git URL, OCI registry
    public ResourceTemplateVersionState State { get; init; }    // Active, Deprecated
}
```

#### ResourceInstance (Current)
```csharp
public sealed record ResourceInstance
{
    public ResourceInstanceId Id { get; init; }
    public string Name { get; init; }
    public ResourceTemplateVersionId TemplateVersionId { get; init; }
    public string? ExistingResourceId { get; init; }
    public ResourceInstanceState State { get; init; }
    public IReadOnlyList<ApplicationId> Consumers { get; }  // ✅ Already supports multiple consumers
    public EnvironmentId EnvironmentId { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
}
```

### Current Gaps

1. **No State Storage**: Where are Terraform outputs stored?
2. **No Matching Logic**: How to determine if a resource can be shared?
3. **No Outputs Tracking**: Where are connection strings, endpoints stored?
4. **No Dependency Management**: How to handle resources that depend on others?
5. **No Resource Discovery**: How does App B find the existing DB from App A?

---

## Key Requirements

### Functional Requirements

1. **FR1: Resource Reuse Detection**
   - System must determine when to create new vs. reuse existing resources
   - Must support both automatic matching and explicit references

2. **FR2: State Persistence**
   - Store Terraform/Pulumi state locations
   - Store resource outputs (connection strings, endpoints, ARNs)
   - Support multiple IaC backends (S3, Azure Blob, Terraform Cloud)

3. **FR3: Consumer Tracking**
   - Track which applications consume which resources
   - Support adding/removing consumers over time
   - Enable cleanup when last consumer is removed

4. **FR4: Dependency Resolution**
   - Handle resource dependencies (DB → Resource Group)
   - Provision in correct order
   - Delete in reverse order

5. **FR5: Flexible Sharing Models**
   - Support dedicated resources (1:1 app-to-resource)
   - Support shared resources (N:1 apps-to-resource)
   - Support environment-scoped sharing (shared within env, not across)

6. **FR6: Resource Lifecycle**
   - Provision → Active → Updating → Deleting → Deleted
   - Handle failed provisioning
   - Support resource updates without recreating

### Non-Functional Requirements

1. **NFR1: Flexibility**
   - Not tied to specific IaC tool
   - Support custom provisioning logic
   - Extensible for new resource types

2. **NFR2: Traceability**
   - Audit trail of who created/modified/deleted resources
   - Track resource lineage (which deployment created it)

3. **NFR3: Consistency**
   - Prevent duplicate resource creation
   - Handle concurrent provisioning requests
   - Maintain referential integrity

---

## Architectural Approaches

### Option 1: Resource Matching by Criteria

#### Concept

Applications declare resource requirements with matching criteria. System automatically finds existing resources that match or creates new ones.

#### Architecture

```
Application Score File (score.yaml):
---
resources:
  database:
    type: azure-cosmosdb
    params:
      tier: Standard
      consistency: Strong
    matching:
      scope: environment        # Match within same environment only
      sharingPolicy: shared     # Allow sharing with other apps
      matchOn:                  # Match if these params are identical
        - tier
        - consistency
```

**Domain Model Changes:**

```csharp
// New: Resource Matching Criteria
public sealed record ResourceMatchingCriteria
{
    public required string Type { get; init; }                    // "azure-cosmosdb"
    public required MatchingScope Scope { get; init; }            // Environment, Organisation, Global
    public required SharingPolicy SharingPolicy { get; init; }    // Dedicated, Shared, Conditional
    public required Dictionary<string, string> MatchParameters { get; init; }  // Params to match on
}

public enum MatchingScope
{
    Environment,    // Only match within same environment
    Organisation,   // Match across organisation
    Global          // Match globally (rare)
}

public enum SharingPolicy
{
    Dedicated,      // Never share, always create new
    Shared,         // Always share if match found
    Conditional     // Share based on additional rules
}

// Enhanced ResourceInstance
public sealed record ResourceInstance
{
    // Existing fields...
    public ResourceInstanceId Id { get; init; }
    public string Name { get; init; }
    public ResourceTemplateVersionId TemplateVersionId { get; init; }
    public EnvironmentId EnvironmentId { get; init; }
    public IReadOnlyList<ApplicationId> Consumers { get; }

    // New fields
    public required ResourceMatchingCriteria MatchingCriteria { get; init; }
    public required Dictionary<string, string> ProvisioningParameters { get; init; }
    public required ResourceStateLocation StateLocation { get; init; }
    public required ResourceOutputs Outputs { get; init; }
    public int MaxConsumers { get; init; } = int.MaxValue;  // Limit concurrent consumers
}

// New: State Location (generic, not Terraform-specific)
public sealed record ResourceStateLocation
{
    public required StateBackendType Backend { get; init; }  // S3, AzureBlob, TerraformCloud, Local
    public required string Location { get; init; }           // URI or path
    public Dictionary<string, string>? Metadata { get; init; }
}

public enum StateBackendType
{
    S3,
    AzureBlob,
    TerraformCloud,
    Local,
    Custom
}

// New: Resource Outputs (connection strings, endpoints, ARNs)
public sealed record ResourceOutputs
{
    private readonly Dictionary<string, OutputValue> _outputs = new();

    public IReadOnlyDictionary<string, OutputValue> Outputs => _outputs.AsReadOnly();

    public void SetOutput(string key, string value, bool sensitive = false)
    {
        _outputs[key] = new OutputValue(value, sensitive);
    }

    public string? GetOutput(string key) => _outputs.TryGetValue(key, out var val) ? val.Value : null;
}

public sealed record OutputValue(string Value, bool Sensitive);
```

**Matching Algorithm:**

```csharp
public interface IResourceMatcher
{
    Task<ResourceInstance?> FindMatchingResourceAsync(
        ResourceMatchingCriteria criteria,
        Dictionary<string, string> parameters,
        EnvironmentId environmentId,
        CancellationToken ct);
}

public class ResourceMatcher : IResourceMatcher
{
    public async Task<ResourceInstance?> FindMatchingResourceAsync(
        ResourceMatchingCriteria criteria,
        Dictionary<string, string> parameters,
        EnvironmentId environmentId,
        CancellationToken ct)
    {
        // 1. Filter by scope
        var candidateResources = await _repository.GetResourcesByTypeAndScopeAsync(
            criteria.Type,
            criteria.Scope,
            environmentId,
            ct);

        // 2. Filter by sharing policy
        candidateResources = candidateResources
            .Where(r => r.MatchingCriteria.SharingPolicy != SharingPolicy.Dedicated)
            .Where(r => r.Consumers.Count < r.MaxConsumers);

        // 3. Match on parameters
        foreach (var resource in candidateResources)
        {
            bool matches = true;
            foreach (var matchParam in criteria.MatchParameters)
            {
                if (!resource.ProvisioningParameters.TryGetValue(matchParam.Key, out var resourceValue) ||
                    resourceValue != matchParam.Value)
                {
                    matches = false;
                    break;
                }
            }

            if (matches)
            {
                return resource;
            }
        }

        return null; // No match found, create new
    }
}
```

**Provisioning Flow:**

```csharp
public class ResourceProvisioningService
{
    private readonly IResourceMatcher _matcher;
    private readonly IResourceInstanceRepository _repository;
    private readonly ITerraformDriver _terraformDriver;

    public async Task<ResourceInstance> ProvisionOrReuseAsync(
        Application application,
        ResourceRequirement requirement,
        EnvironmentId environmentId,
        CancellationToken ct)
    {
        // 1. Try to find existing resource
        var existingResource = await _matcher.FindMatchingResourceAsync(
            requirement.MatchingCriteria,
            requirement.Parameters,
            environmentId,
            ct);

        if (existingResource is not null)
        {
            // 2. Add application as consumer
            existingResource.AddConsumer(application.Id);
            await _repository.UpdateAsync(existingResource, ct);

            _logger.LogInformation(
                "Reusing existing resource {ResourceId} for application {AppId}",
                existingResource.Id, application.Id);

            return existingResource;
        }

        // 3. No match, provision new resource
        var newResource = ResourceInstance.Create(
            name: $"{requirement.Type}-{environmentId.Value}-{Guid.NewGuid().ToString()[..8]}",
            templateVersionId: requirement.TemplateVersionId,
            environmentId: environmentId,
            matchingCriteria: requirement.MatchingCriteria,
            provisioningParameters: requirement.Parameters);

        newResource.AddConsumer(application.Id);

        // 4. Store in database as "Provisioning"
        await _repository.CreateAsync(newResource, ct);

        // 5. Provision via Terraform (in Worker)
        var planResult = await _terraformDriver.PlanAsync(
            new List<TerraformPlanInput> { TerraformPlanInput.FromResourceInstance(newResource) },
            folderName: newResource.Id.Value.ToString());

        await _terraformDriver.ApplyAsync(planResult);

        // 6. Extract outputs and update resource
        var outputs = await ExtractTerraformOutputsAsync(planResult.StateDirectory);
        newResource.Outputs.SetOutputs(outputs);
        newResource.StateLocation = new ResourceStateLocation
        {
            Backend = StateBackendType.AzureBlob,
            Location = $"azblob://tfstate/{newResource.Id}.tfstate"
        };

        await _repository.UpdateAsync(newResource, ct);

        return newResource;
    }
}
```

#### Pros

✅ **Automatic Resource Sharing**: System handles matching logic automatically
✅ **Flexible Criteria**: Configure matching rules per resource type
✅ **No Manual Coordination**: Developers don't need to know about existing resources
✅ **Consistent Naming**: System generates consistent resource names
✅ **Audit Trail**: Clear history of which app triggered resource creation

#### Cons

❌ **Complex Matching Logic**: Algorithm can be complex with many parameters
❌ **Ambiguity**: Multiple potential matches require tiebreaking logic
❌ **Performance**: Matching queries can be expensive at scale
❌ **Debugging**: Hard to predict which resource will match
❌ **Race Conditions**: Two apps might create duplicate resources if provisioning simultaneously

#### Complexity Score: 7/10

---

### Option 2: Explicit Resource References

#### Concept

Applications explicitly reference resources by name or ID. System creates resources on first reference and links subsequent references.

#### Architecture

**Application Score File (score.yaml):**
```yaml
resources:
  database:
    type: azure-cosmosdb
    params:
      tier: Standard
      consistency: Strong
    reference:
      mode: shared              # shared, dedicated
      name: "shared-cosmos-db"  # Explicit name for sharing
```

**Domain Model:**

```csharp
public sealed record ResourceReference
{
    public required ResourceReferenceMode Mode { get; init; }
    public string? Name { get; init; }           // For shared mode
    public ResourceInstanceId? InstanceId { get; init; }  // For explicit instance reference
}

public enum ResourceReferenceMode
{
    Dedicated,      // Create unique resource for this app
    Shared,         // Share with other apps using same name
    ExistingId      // Reference existing resource by ID
}

public sealed record ResourceInstance
{
    // Existing fields...
    public ResourceInstanceId Id { get; init; }
    public string Name { get; init; }  // Used for shared resource discovery
    public ResourceTemplateVersionId TemplateVersionId { get; init; }
    public EnvironmentId EnvironmentId { get; init; }
    public IReadOnlyList<ApplicationId> Consumers { get; }

    // New fields
    public required ResourceReferenceMode ReferenceMode { get; init; }
    public required Dictionary<string, string> ProvisioningParameters { get; init; }
    public required ResourceStateLocation StateLocation { get; init; }
    public required ResourceOutputs Outputs { get; init; }
}
```

**Provisioning Logic:**

```csharp
public class ResourceProvisioningService
{
    public async Task<ResourceInstance> ProvisionOrLinkAsync(
        Application application,
        ResourceRequirement requirement,
        EnvironmentId environmentId,
        CancellationToken ct)
    {
        switch (requirement.Reference.Mode)
        {
            case ResourceReferenceMode.Dedicated:
                // Always create new resource
                return await ProvisionNewResourceAsync(
                    application,
                    requirement,
                    environmentId,
                    generateUniqueName: true,
                    ct);

            case ResourceReferenceMode.Shared:
                // Find or create by name
                var sharedResource = await _repository.GetByNameAndEnvironmentAsync(
                    requirement.Reference.Name!,
                    environmentId,
                    ct);

                if (sharedResource is not null)
                {
                    // Add as consumer
                    sharedResource.AddConsumer(application.Id);
                    await _repository.UpdateAsync(sharedResource, ct);
                    return sharedResource;
                }

                // Create new with specified name
                return await ProvisionNewResourceAsync(
                    application,
                    requirement,
                    environmentId,
                    customName: requirement.Reference.Name!,
                    ct);

            case ResourceReferenceMode.ExistingId:
                // Link to existing resource by ID
                var existingResource = await _repository.GetByIdAsync(
                    requirement.Reference.InstanceId!,
                    ct);

                if (existingResource is null)
                {
                    throw new ResourceNotFoundException(requirement.Reference.InstanceId!);
                }

                existingResource.AddConsumer(application.Id);
                await _repository.UpdateAsync(existingResource, ct);
                return existingResource;

            default:
                throw new ArgumentOutOfRangeException();
        }
    }
}
```

#### Pros

✅ **Simple & Predictable**: Explicit naming makes behavior clear
✅ **Developer Control**: Developers decide sharing strategy
✅ **Easy Debugging**: Name-based lookup is straightforward
✅ **No Ambiguity**: Exact match by name or ID
✅ **Fast Lookups**: Simple index on (Name, EnvironmentId)

#### Cons

❌ **Manual Coordination**: Developers must agree on resource names
❌ **Naming Conflicts**: Risk of accidental name collisions
❌ **No Validation**: Can't enforce parameter compatibility between apps
❌ **Brittle**: Renaming resources breaks references
❌ **Documentation Burden**: Need to document shared resource names

#### Complexity Score: 4/10

---

### Option 3: Resource Pools

#### Concept

Pre-provision pools of resources. Applications claim resources from pools. Supports both dedicated and shared models.

#### Architecture

**Resource Pool Definition:**
```yaml
# Resource Pool Config
pools:
  - name: cosmos-db-standard-pool
    environment: production
    resourceTemplate: azure-cosmosdb
    version: v1.0.0
    size: 3                    # Pre-provision 3 instances
    sharingPolicy: shared
    parameters:
      tier: Standard
      consistency: Strong
```

**Domain Model:**

```csharp
public sealed record ResourcePool
{
    public required ResourcePoolId Id { get; init; }
    public required string Name { get; init; }
    public required EnvironmentId EnvironmentId { get; init; }
    public required ResourceTemplateVersionId TemplateVersionId { get; init; }
    public required int DesiredSize { get; init; }
    public required SharingPolicy SharingPolicy { get; init; }
    public required Dictionary<string, string> Parameters { get; init; }

    private readonly List<ResourceInstance> _instances = new();
    public IReadOnlyList<ResourceInstance> Instances => _instances.AsReadOnly();

    public int AvailableCount => _instances.Count(i =>
        i.State == ResourceInstanceState.Active &&
        (SharingPolicy == SharingPolicy.Shared || i.Consumers.Count == 0));
}

public class ResourcePoolManager
{
    public async Task<ResourceInstance> ClaimFromPoolAsync(
        ResourcePool pool,
        Application application,
        CancellationToken ct)
    {
        // 1. Find available resource
        var availableResource = pool.Instances
            .FirstOrDefault(i =>
                i.State == ResourceInstanceState.Active &&
                (pool.SharingPolicy == SharingPolicy.Shared || i.Consumers.Count == 0));

        if (availableResource is null)
        {
            // 2. Pool exhausted, provision on-demand
            availableResource = await ProvisionNewResourceInPoolAsync(pool, ct);
        }

        // 3. Claim resource
        availableResource.AddConsumer(application.Id);
        await _repository.UpdateAsync(availableResource, ct);

        return availableResource;
    }

    public async Task MaintainPoolSizeAsync(ResourcePool pool, CancellationToken ct)
    {
        var activeCount = pool.Instances.Count(i => i.State == ResourceInstanceState.Active);

        if (activeCount < pool.DesiredSize)
        {
            var toProvision = pool.DesiredSize - activeCount;
            for (int i = 0; i < toProvision; i++)
            {
                await ProvisionNewResourceInPoolAsync(pool, ct);
            }
        }
    }
}
```

**Provisioning Flow:**

```
Application Deployment
  ↓
Check Resource Requirement
  ↓
Find Matching Pool
  ↓
Claim Resource from Pool
  ↓
If Pool Empty → Provision On-Demand
  ↓
Add App as Consumer
```

#### Pros

✅ **Fast Provisioning**: Resources pre-created, ready to use
✅ **Predictable Capacity**: Know exactly how many resources available
✅ **Cost Optimization**: Pool size tuned to demand
✅ **Simple Claiming**: Just assign next available resource
✅ **Cleanup Management**: Pool manager handles lifecycle

#### Cons

❌ **Over-Provisioning**: Unused resources cost money
❌ **Configuration Overhead**: Need to define and manage pools
❌ **Inflexibility**: Hard to support custom parameters per app
❌ **Pool Exhaustion**: Need fallback for when pool is empty
❌ **Complex Cleanup**: When to shrink pool?

#### Complexity Score: 6/10

---

### Option 4: Declarative Resource Graph

#### Concept

Applications declare a graph of required resources with dependencies. System resolves graph, provisions, and tracks relationships.

#### Architecture

**Application Score File (score.yaml):**
```yaml
resources:
  resource-group:
    type: azure-resource-group
    sharing: shared             # Shared across all apps in environment
    params:
      location: westus2

  cosmos-db:
    type: azure-cosmosdb
    sharing: shared             # Shared if params match
    dependsOn:
      - resource-group
    params:
      tier: Standard
      consistency: Strong
      resourceGroupId: ${resources.resource-group.id}

  blob-storage:
    type: azure-blob-storage
    sharing: dedicated          # Each app gets own storage
    dependsOn:
      - resource-group
    params:
      replication: LRS
      resourceGroupId: ${resources.resource-group.id}
```

**Domain Model:**

```csharp
public sealed record ResourceDependency
{
    public required ResourceInstanceId DependentId { get; init; }      // Resource that depends
    public required ResourceInstanceId DependencyId { get; init; }     // Resource depended upon
    public required DependencyType Type { get; init; }
}

public enum DependencyType
{
    StrongReference,    // Must exist before provisioning
    WeakReference,      // Optional dependency
    OutputReference     // Uses outputs from dependency
}

public sealed record ResourceInstance
{
    // Existing fields...
    public ResourceInstanceId Id { get; init; }
    public string Name { get; init; }
    public ResourceTemplateVersionId TemplateVersionId { get; init; }
    public EnvironmentId EnvironmentId { get; init; }
    public IReadOnlyList<ApplicationId> Consumers { get; }
    public ResourceStateLocation StateLocation { get; init; }
    public ResourceOutputs Outputs { get; init; }

    // New: Dependency tracking
    public IReadOnlyList<ResourceDependency> Dependencies { get; }
    public IReadOnlyList<ResourceDependency> Dependents { get; }

    // New: Graph metadata
    public required SharingMode SharingMode { get; init; }
    public Dictionary<string, string> ProvisioningParameters { get; init; }
}

public enum SharingMode
{
    Dedicated,          // Never share
    SharedByName,       // Share if name matches
    SharedByParams,     // Share if params match
    SharedAlways        // Always share first available
}
```

**Resource Graph Builder:**

```csharp
public class ResourceGraphBuilder
{
    public async Task<ResourceGraph> BuildGraphAsync(
        Application application,
        IEnumerable<ResourceRequirement> requirements,
        EnvironmentId environmentId,
        CancellationToken ct)
    {
        var graph = new ResourceGraph();
        var resolvedResources = new Dictionary<string, ResourceInstance>();

        // 1. Topological sort based on dependencies
        var sorted = TopologicalSort(requirements);

        // 2. Process each requirement in dependency order
        foreach (var requirement in sorted)
        {
            // 3. Resolve dependencies first
            var resolvedDeps = new List<ResourceInstance>();
            foreach (var depName in requirement.DependsOn)
            {
                if (!resolvedResources.TryGetValue(depName, out var dep))
                {
                    throw new InvalidOperationException($"Dependency {depName} not resolved");
                }
                resolvedDeps.Add(dep);
            }

            // 4. Substitute dependency outputs into parameters
            var parameters = SubstituteDependencyOutputs(
                requirement.Parameters,
                resolvedResources);

            // 5. Find or create resource
            var resource = await FindOrCreateResourceAsync(
                requirement,
                parameters,
                environmentId,
                resolvedDeps,
                ct);

            resolvedResources[requirement.Name] = resource;
            graph.AddResource(resource);
        }

        return graph;
    }

    private Dictionary<string, string> SubstituteDependencyOutputs(
        Dictionary<string, string> parameters,
        Dictionary<string, ResourceInstance> resolvedResources)
    {
        var substituted = new Dictionary<string, string>();

        foreach (var (key, value) in parameters)
        {
            // Handle ${resources.resource-group.id} syntax
            if (value.StartsWith("${resources."))
            {
                var parts = value.Trim('$', '{', '}').Split('.');
                var resourceName = parts[1];
                var outputKey = parts[2];

                if (resolvedResources.TryGetValue(resourceName, out var resource))
                {
                    var outputValue = resource.Outputs.GetOutput(outputKey);
                    substituted[key] = outputValue ?? value;
                }
            }
            else
            {
                substituted[key] = value;
            }
        }

        return substituted;
    }
}
```

**Provisioning Flow:**

```
1. Parse Score File → Extract resource requirements
2. Build dependency graph
3. Topological sort (ensure dependencies provisioned first)
4. For each resource (in order):
   a. Check if matching resource exists
   b. If exists: add consumer, extract outputs
   c. If not: provision new resource
   d. Record dependencies
5. Return resource graph with all outputs
```

**Cleanup Flow:**

```csharp
public class ResourceCleanupService
{
    public async Task CleanupApplicationResourcesAsync(
        Application application,
        EnvironmentId environmentId,
        CancellationToken ct)
    {
        // 1. Find all resources consumed by this app
        var resources = await _repository.GetResourcesByConsumerAsync(
            application.Id,
            environmentId,
            ct);

        // 2. Build dependency graph
        var graph = BuildDependencyGraph(resources);

        // 3. Remove app as consumer
        foreach (var resource in resources)
        {
            resource.RemoveConsumer(application.Id);
        }

        // 4. Find resources with no consumers (reverse topological order)
        var toDelete = graph.GetNodesInReverseTopologicalOrder()
            .Where(r => r.Consumers.Count == 0);

        // 5. Delete resources (dependencies last)
        foreach (var resource in toDelete)
        {
            await DeleteResourceAsync(resource, ct);
        }
    }
}
```

#### Pros

✅ **Powerful Dependency Management**: Handles complex resource relationships
✅ **Output Substitution**: Auto-wire outputs between resources
✅ **Correct Provisioning Order**: Guarantees dependencies created first
✅ **Clean Deletion**: Deletes in reverse order, respects dependencies
✅ **Flexible Sharing**: Mix shared and dedicated resources

#### Cons

❌ **High Complexity**: Graph algorithms, cycle detection, topological sort
❌ **Debugging Difficulty**: Hard to trace provisioning failures
❌ **Performance**: Graph operations expensive for large resource sets
❌ **Learning Curve**: Developers need to understand dependency syntax
❌ **Circular Dependencies**: Need cycle detection and error handling

#### Complexity Score: 9/10

---

## State Storage Strategies

Regardless of resource matching approach, we need to store infrastructure state.

### Strategy 1: Embedded State (in Database)

Store all state directly in the database.

```csharp
public sealed record ResourceInstance
{
    // ...existing fields

    // Embedded state
    public string? TerraformStateJson { get; init; }  // Full Terraform state
    public Dictionary<string, string> Outputs { get; init; }
    public Dictionary<string, string> Parameters { get; init; }
}
```

**Pros:**
✅ Single source of truth
✅ Easy backup/restore
✅ Simple queries

**Cons:**
❌ Large database size
❌ Terraform state can be >1MB
❌ Sensitive data in database
❌ Versioning difficult

### Strategy 2: External State with References

Store state in external backend, reference from database.

```csharp
public sealed record ResourceStateLocation
{
    public required StateBackendType Backend { get; init; }
    public required string Location { get; init; }
    public Dictionary<string, string>? Credentials { get; init; }
}

public sealed record ResourceInstance
{
    // ...existing fields

    public required ResourceStateLocation StateLocation { get; init; }
    public Dictionary<string, string> Outputs { get; init; }  // Only outputs in DB
}
```

**Terraform Configuration:**
```hcl
terraform {
  backend "azurerm" {
    storage_account_name = "conductorstate"
    container_name       = "tfstate"
    key                  = "resources/${resource_instance_id}.tfstate"
  }
}
```

**Pros:**
✅ Small database size
✅ Terraform-native storage
✅ State versioning built-in
✅ Credentials separate

**Cons:**
❌ External dependency
❌ Need credentials management
❌ Backup complexity
❌ Cross-region access latency

**Recommendation:** Use Strategy 2 (External State) for scalability and Terraform compatibility.

---

## Comparison Matrix

| Criteria | Option 1: Matching | Option 2: Explicit Refs | Option 3: Pools | Option 4: Graph |
|----------|-------------------|------------------------|----------------|-----------------|
| **Complexity** | ⭐⭐⭐⭐⭐⭐⭐ High | ⭐⭐⭐⭐ Low | ⭐⭐⭐⭐⭐⭐ Medium | ⭐⭐⭐⭐⭐⭐⭐⭐⭐ Very High |
| **Developer Experience** | ⭐⭐⭐⭐⭐ Excellent | ⭐⭐⭐ Good | ⭐⭐⭐ Good | ⭐⭐ Challenging |
| **Predictability** | ⭐⭐ Poor | ⭐⭐⭐⭐⭐ Excellent | ⭐⭐⭐⭐ Good | ⭐⭐⭐ Moderate |
| **Performance** | ⭐⭐⭐ Moderate | ⭐⭐⭐⭐⭐ Excellent | ⭐⭐⭐⭐⭐ Excellent | ⭐⭐ Poor |
| **Flexibility** | ⭐⭐⭐⭐⭐ Excellent | ⭐⭐⭐ Good | ⭐⭐ Poor | ⭐⭐⭐⭐⭐ Excellent |
| **Shared Resources** | ✅ Automatic | ✅ Manual | ✅ Pool-based | ✅ Declarative |
| **Dedicated Resources** | ✅ Via policy | ✅ Default | ✅ Via pool config | ✅ Via sharing mode |
| **Dependencies** | ❌ Limited | ❌ None | ❌ None | ✅ Full support |
| **Race Conditions** | ⚠️ High risk | ⚠️ Low risk | ⚠️ Low risk | ⚠️ Medium risk |
| **Debug Ease** | ⭐⭐ Difficult | ⭐⭐⭐⭐⭐ Easy | ⭐⭐⭐⭐ Good | ⭐⭐ Difficult |
| **Implementation Time** | 3-4 weeks | 1-2 weeks | 2-3 weeks | 4-6 weeks |
| **Best For** | Auto-discovery | Simple cases | Pre-planned capacity | Complex dependencies |

---

## Recommendation

### For Most Teams: **Option 2 (Explicit Resource References) + Simplified Dependency Support**

**Rationale:**

1. **Predictable & Simple**: Explicit naming makes behavior clear and debugging easy

2. **Fast Implementation**: Can be built in 1-2 weeks on top of existing domain models

3. **Developer-Friendly**: Developers have full control, no magic matching

4. **Good Enough for 80% of Use Cases**: Most scenarios are:
   - Dedicated resources (each app gets own DB)
   - Simple shared resources (multiple apps share one DB via name)

5. **Extensible**: Can add basic dependency support later without full graph complexity

### Enhanced Recommendation: Option 2 + Basic Dependencies

Add simple dependency support to Option 2:

```yaml
resources:
  resource-group:
    type: azure-resource-group
    reference:
      mode: shared
      name: "prod-rg"
    params:
      location: westus2

  cosmos-db:
    type: azure-cosmosdb
    reference:
      mode: shared
      name: "shared-cosmos"
    dependsOn:
      - resource-group    # Simple dependency list
    params:
      tier: Standard
      resourceGroupId: ${resource-group.id}  # Output substitution
```

**Implementation:**

```csharp
public class ResourceProvisioningService
{
    public async Task<Dictionary<string, ResourceInstance>> ProvisionResourcesAsync(
        Application application,
        IEnumerable<ResourceRequirement> requirements,
        EnvironmentId environmentId,
        CancellationToken ct)
    {
        var resolved = new Dictionary<string, ResourceInstance>();

        // Simple dependency resolution: process in order, resolve deps on-the-fly
        var queue = new Queue<ResourceRequirement>(requirements);
        var retryQueue = new Queue<ResourceRequirement>();

        while (queue.Count > 0)
        {
            var requirement = queue.Dequeue();

            // Check if dependencies are resolved
            var depsResolved = requirement.DependsOn.All(dep => resolved.ContainsKey(dep));

            if (!depsResolved)
            {
                // Defer to retry queue
                retryQueue.Enqueue(requirement);
                continue;
            }

            // Substitute dependency outputs
            var parameters = SubstituteOutputs(requirement.Parameters, resolved);

            // Provision or link resource
            var resource = await ProvisionOrLinkAsync(
                application,
                requirement,
                parameters,
                environmentId,
                ct);

            resolved[requirement.Name] = resource;
        }

        // Process retry queue
        if (retryQueue.Count > 0)
        {
            foreach (var req in retryQueue)
            {
                queue.Enqueue(req);
            }
            // Continue processing...
        }

        return resolved;
    }
}
```

---

## Implementation Guide

### Phase 1: Core Infrastructure (Week 1-2)

1. **Extend ResourceInstance Domain Model**
```csharp
public sealed record ResourceInstance
{
    // Existing
    public ResourceInstanceId Id { get; init; }
    public string Name { get; init; }
    public ResourceTemplateVersionId TemplateVersionId { get; init; }
    public EnvironmentId EnvironmentId { get; init; }
    public IReadOnlyList<ApplicationId> Consumers { get; }

    // New
    public required ResourceReferenceMode ReferenceMode { get; init; }
    public required Dictionary<string, string> ProvisioningParameters { get; init; }
    public required ResourceStateLocation StateLocation { get; init; }
    public required ResourceOutputs Outputs { get; init; }
    public ResourceInstanceState State { get; private set; }
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; private set; }
}
```

2. **Create State Storage Models**
```csharp
public sealed record ResourceStateLocation(
    StateBackendType Backend,
    string Location,
    Dictionary<string, string>? Metadata);

public sealed record ResourceOutputs
{
    private Dictionary<string, OutputValue> _outputs = new();
    public IReadOnlyDictionary<string, OutputValue> Outputs => _outputs;

    public void SetOutput(string key, string value, bool sensitive = false);
    public string? GetOutput(string key);
}
```

3. **Database Migration**
```bash
./scripts/efm.sh AddResourceInstanceEnhancements
```

### Phase 2: Provisioning Logic (Week 2-3)

4. **Implement ResourceProvisioningService**
   - `ProvisionOrLinkAsync()`
   - `FindResourceByNameAsync()`
   - `AddConsumerAsync()`

5. **Update Worker to Store Outputs**
   - Extract Terraform outputs after apply
   - Store in ResourceOutputs
   - Update StateLocation

6. **Create Repository Methods**
   - `GetByNameAndEnvironmentAsync()`
   - `GetByConsumerAsync()`
   - `UpdateOutputsAsync()`

### Phase 3: API Endpoints (Week 3-4)

7. **Resource Instance Endpoints**
   - `GET /resources` - List all resources in environment
   - `GET /resources/{id}` - Get resource details + outputs
   - `POST /resources/{id}/consumers` - Add consumer
   - `DELETE /resources/{id}/consumers/{appId}` - Remove consumer

8. **Deployment Integration**
   - Update `CreateDeploymentEndpoint` to parse Score file
   - Extract resource requirements
   - Call `ResourceProvisioningService`

### Phase 4: Cleanup & Dependencies (Week 4-5)

9. **Resource Cleanup**
   - `RemoveConsumerAsync()`
   - Delete resource when last consumer removed
   - Handle Terraform destroy

10. **Basic Dependency Support** (Optional)
    - Parse `dependsOn` from Score file
    - Simple ordering algorithm
    - Output substitution

---

## Appendix

### Example: Day 1 → Day 7 Flow

**Day 1: Application A Deployment**

1. App A Score file:
```yaml
resources:
  database:
    type: azure-cosmosdb
    reference:
      mode: shared
      name: "shared-cosmos-db"
    params:
      tier: Standard
```

2. Worker processes deployment:
   - Looks for resource named "shared-cosmos-db" in environment
   - Not found → provisions new Cosmos DB
   - Stores in database:
     - Name: "shared-cosmos-db"
     - Consumers: [AppA]
     - Outputs: { connectionString: "...", endpoint: "..." }
     - StateLocation: "azblob://tfstate/cosmos-abc123.tfstate"

**Day 7: Application B Deployment**

1. App B Score file:
```yaml
resources:
  database:
    type: azure-cosmosdb
    reference:
      mode: shared
      name: "shared-cosmos-db"  # Same name!
    params:
      tier: Standard
```

2. Worker processes deployment:
   - Looks for resource named "shared-cosmos-db" in environment
   - **Found!** → No provisioning needed
   - Adds App B as consumer:
     - Consumers: [AppA, AppB]
   - Returns existing outputs to App B

**Day 14: Application A Deleted**

1. Cleanup service:
   - Removes App A from consumers list
   - Consumers: [AppB]
   - Cosmos DB **NOT deleted** (still has consumer)

**Day 21: Application B Deleted**

1. Cleanup service:
   - Removes App B from consumers list
   - Consumers: []
   - **No consumers left** → Triggers Terraform destroy
   - Deletes Cosmos DB

---

### Score File Examples

**Example 1: Dedicated Resources**
```yaml
resources:
  storage:
    type: azure-blob-storage
    reference:
      mode: dedicated  # Each app gets own storage
    params:
      replication: LRS
```

**Example 2: Shared Database**
```yaml
resources:
  database:
    type: azure-cosmosdb
    reference:
      mode: shared
      name: "team-shared-db"
    params:
      tier: Standard
      consistency: Strong
```

**Example 3: With Dependencies**
```yaml
resources:
  resource-group:
    type: azure-resource-group
    reference:
      mode: shared
      name: "prod-rg"
    params:
      location: westus2

  storage:
    type: azure-blob-storage
    reference:
      mode: dedicated
    dependsOn:
      - resource-group
    params:
      replication: LRS
      resourceGroupId: ${resource-group.id}
```

---

### Database Schema

```sql
-- ResourceInstances table
CREATE TABLE ResourceInstances (
    Id UUID PRIMARY KEY,
    Name VARCHAR(255) NOT NULL,
    TemplateVersionId UUID NOT NULL,
    EnvironmentId UUID NOT NULL,
    ReferenceMode VARCHAR(50) NOT NULL,
    State VARCHAR(50) NOT NULL,
    StateBackend VARCHAR(50),
    StateLocation TEXT,
    ExistingResourceId VARCHAR(255),
    CreatedAt TIMESTAMP NOT NULL,
    UpdatedAt TIMESTAMP NOT NULL,
    UNIQUE(Name, EnvironmentId)  -- Enforce unique names per environment
);

-- ResourceInstanceConsumers (many-to-many)
CREATE TABLE ResourceInstanceConsumers (
    ResourceInstanceId UUID NOT NULL,
    ApplicationId UUID NOT NULL,
    AddedAt TIMESTAMP NOT NULL,
    PRIMARY KEY (ResourceInstanceId, ApplicationId)
);

-- ResourceInstanceOutputs (key-value pairs)
CREATE TABLE ResourceInstanceOutputs (
    ResourceInstanceId UUID NOT NULL,
    OutputKey VARCHAR(255) NOT NULL,
    OutputValue TEXT NOT NULL,
    Sensitive BOOLEAN DEFAULT FALSE,
    PRIMARY KEY (ResourceInstanceId, OutputKey)
);

-- ResourceInstanceParameters (key-value pairs)
CREATE TABLE ResourceInstanceParameters (
    ResourceInstanceId UUID NOT NULL,
    ParameterKey VARCHAR(255) NOT NULL,
    ParameterValue TEXT NOT NULL,
    PRIMARY KEY (ResourceInstanceId, ParameterKey)
);

-- ResourceDependencies (optional, for Option 4)
CREATE TABLE ResourceDependencies (
    DependentId UUID NOT NULL,
    DependencyId UUID NOT NULL,
    DependencyType VARCHAR(50) NOT NULL,
    PRIMARY KEY (DependentId, DependencyId)
);
```

---

**Questions? Ready to proceed with implementation?**
