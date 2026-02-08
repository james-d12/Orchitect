# Domain Models

## Resource

Represents a provisioned resource instance (e.g., CosmosDB, VNet, Helm release, Kubernetes Pod).

```csharp
using Orchitect.Domain.Environment;
using Orchitect.Domain.ResourceTemplate;

namespace Orchitect.Domain.Resource;

public sealed record Resource
{
    public required ResourceId Id { get; init; }
    public required string Name { get; init; }

    // The blueprint this resource is based on
    public required ResourceTemplateId ResourceTemplateId { get; init; }

    // The scope in which this resource lives (app, env, or global)
    public required ResourceScope Scope { get; init; }

    // Lifecycle metadata
    public required DateTime CreatedAt { get; init; }
    public required DateTime UpdatedAt { get; init; }

    // Flexible "instance details" (runtime values, identifiers, outputs)
    public IDictionary<string, object>? Properties { get; init; }

    // Current state of the resource
    public ResourceState State { get; init; } = ResourceState.Pending;
}

public enum ResourceState
{
    Pending,
    Provisioning,
    Active,
    Updating,
    Failed,
    PendingDeletion,
    Deleted
}

public sealed record ResourceScope
{
    public ApplicationId? ApplicationId { get; init; }
    public required EnvironmentId EnvironmentId { get; init; }
}
```

### Examples

#### App and Environment Scoped Resource
```json
{
  "id": "res-123",
  "name": "payment-cosmos",
  "resourceTemplateId": "tmpl-cosmosdb",
  "scope": {
    "applicationId": "app-payment-api",
    "environmentId": "env-dev"
  },
  "createdAt": "2025-09-27T12:00:00Z",
  "updatedAt": "2025-09-27T12:00:00Z",
  "properties": {
    "connectionString": "AccountEndpoint=https://payment-cosmos.documents.azure.com:443/;",
    "throughput": 400
  },
  "state": "Active"
}
```

#### Environment Scoped Resource
```json
{
  "id": "res-456",
  "name": "dev-vnet",
  "resourceTemplateId": "tmpl-virtual-network",
  "scope": {
    "applicationId": null,
    "environmentId": "env-dev"
  },
  "createdAt": "2025-09-27T12:10:00Z",
  "updatedAt": "2025-09-27T12:10:00Z",
  "properties": {
    "cidrBlock": "10.0.0.0/16",
    "subnets": ["10.0.1.0/24", "10.0.2.0/24"]
  },
  "state": "Active"
}
```

#### Global Scoped Resource
```json
{
  "id": "res-789",
  "name": "company-dns-zone",
  "resourceTemplateId": "tmpl-dns-zone",
  "scope": {
    "applicationId": null,
    "environmentId": null
  },
  "createdAt": "2025-09-27T12:20:00Z",
  "updatedAt": "2025-09-27T12:20:00Z",
  "properties": {
    "zoneName": "company.com",
    "nameServers": ["ns1.company.com", "ns2.company.com"]
  },
  "state": "Active"
}
```