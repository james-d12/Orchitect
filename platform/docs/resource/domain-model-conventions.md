---
title: "Engine domain model conventions"
status: reference
workstream: resource
milestone: "Resource Domain"
issues: []
superseded_by: null
last_reviewed: 2026-10-02
---

# Engine domain model conventions

Engine aggregates come in two shapes. Both are deliberate: the shape says how the aggregate changes. This was decided as Option A in [`archive/RESOURCE_DOMAIN_PROVISIONING_PHASE_1_OUTSTANDING.md`](../archive/RESOURCE_DOMAIN_PROVISIONING_PHASE_1_OUTSTANDING.md) §7a (#139): `Application` keeps its shape, and the convention below explains why.

## The two shapes

| | Immutable aggregate | Aggregate with guarded in-place mutation |
|---|---|---|
| Properties | `required … { get; init; }` | `{ get; private init; }` for fields fixed at creation, `{ get; private set; }` for fields that change |
| Construction | `private` constructor plus a static `Create(...)` factory that validates | `private` constructor plus a static `Create(request)` factory that validates |
| Changing it | A method returns a new instance via `with` (e.g. `Application.Update`, `Deployment.Start`) | A `void` method changes the instance and enforces the rule (e.g. `ResourceInstance.Transition`, `Resource.AddConsumer`) |
| Persisting a change | Pass the returned instance to the repository's `UpdateAsync` | Pass the same instance to the repository's `UpdateAsync` |
| Examples | `Application`, `Environment`, `Deployment` | `Resource`, `ResourceInstance`, `ResourceTemplate` |

`ResourceDependencyGraph` is a class rather than a record, but follows the second shape: private state changed only through its methods.

## Choosing a shape

- Use the **immutable** shape by default. It suits aggregates whose changes replace a few scalar fields. Callers keep the old value, which makes before/after comparisons and tests simple.
- Use **guarded in-place mutation** when the aggregate owns a collection it adds to (`Resource` consumers, `ResourceTemplate` versions), or when a method enforces a rule on its own state, such as a transition table (`ResourceInstance`).

## Rules for both

- No public setters. Every change goes through a factory or a method on the aggregate, which validates its input.
- Don't mix the shapes inside one aggregate. If an immutable aggregate gains a collection or needs in-place changes, move all of it to the second shape.
- Strongly-typed IDs are created in the factory (`new ApplicationId()`), never by callers.
