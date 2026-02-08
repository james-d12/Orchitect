Platform Orchestrator – Conversation Summary
Goal

Design a platform orchestrator inspired by Humanitec. It should provision resources using any backend (Terraform, Pulumi, custom code, cloud SDKs) and track state generically. The domain needs templates, versions, provisioned instances, environments, and applications.

Resource Templates

A ResourceTemplate represents the blueprint of a resource type (e.g., Cosmos DB, PostgreSQL, Blob Storage).

A ResourceTemplateVersion points to a concrete implementation version, such as:

a git repository + tag

a Helm chart version

a Terraform module version

a Pulumi package version

Versions should have a unique ID (ResourceTemplateVersionId).

Template version fields:

Id (new)

TemplateId

Version string (e.g., "v1", "v2")

Source (git URL, OCI registry, etc.)

Notes

State (Active, Deprecated)

CreatedAt

Resource Instances

A ResourceInstance represents a provisioned resource. It is created from a ResourceTemplateVersion.

Fields discussed:

Id

Name

TemplateVersionId (needs the version to have its own ID)

EnvironmentId (where the resource lives)

Consumers (list of Applications that use this resource)

A StateLocation (generic, not Terraform-specific)

Optional ExistingResourceId (if linking external resources)

StateLocation replaces Terraform-specific fields and is generic:

A URI or structured object representing where provisioning state lives

Example: s3://bucket/key.tfstate, or https://my-api/state/123

ResourceInstance should not also include ApplicationId directly; Applications are consumers, so EnvironmentId stays and Consumers is a list.

Why EnvironmentId but not ApplicationId

A ResourceInstance belongs to exactly one Environment (e.g., dev, staging, prod).
But many applications can consume the same resource.

Therefore Structure:
ResourceInstance:

belongs to EnvironmentId

is optionally consumed by many ApplicationIds

Separation of concerns

ResourceTemplate defines what a resource is.
ResourceTemplateVersion defines how to provision it.
ResourceInstance represents a provisioned resource.
StateLocation represents where its provisioning state lives.

Additional notes

You decided:

to rename ProvisionedResource to ResourceInstance

to add a generic state location instead of TerraformStateUri

to introduce ResourceTemplateVersionId because versions must be first-class

to create new domain objects instead of overloading existing ones

This reflects Humanitec’s model:

Resource Definition (your Template)

Resource Definition Version (your TemplateVersion)

Resource (your ResourceInstance)

Example mode: 

```csharp
public sealed record ResourceInstance
{
    public required ResourceInstanceId Id { get; init; }
    public required string Name { get; init; }

    public required TemplateReference Template { get; init; }
    public string? ExistingResourceId { get; init; }

    public required StateLocation State { get; init; }

    public required ResourceConsumers Consumers { get; init; } = new ResourceConsumers();

    public required EnvironmentId EnvironmentId { get; init; }
    public required DateTime CreatedAt { get; init; }
    public required DateTime UpdatedAt { get; init; }

    public void AddConsumer(ApplicationId appId) => Consumers.Add(appId);
}
```