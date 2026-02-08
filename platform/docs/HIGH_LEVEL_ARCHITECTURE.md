# Conductor Platform – High‑Level Architecture

## Overview

The **Conductor Platform** is a modular internal developer platform composed of a small shared **Core** and multiple independent **capabilities**. The platform is the product boundary; individual capabilities deliver specific functionality while sharing common concepts defined by Core.

This structure allows Conductor to scale horizontally (new capabilities) without tight coupling or architectural erosion.

---

## Platform vs Architecture

* **Platform**: The full Conductor product (Core + all capabilities)
* **Architecture**: How the platform is implemented internally

> Platform is how users understand the system. Architecture is how we build it.

---

## Core

### Purpose

Core defines the **shared language and state** of the platform. It owns the fundamental concepts that all capabilities rely on.

Core does **not** perform work like discovery, deployment, or analysis.

### Core Responsibilities

* Organisation / Tenant
* Identity & ownership
* Application / Service
* Repository
* Shared policies (future)
* Cross‑cutting contracts & invariants

### Core Non‑Responsibilities

* Resource discovery
* Infrastructure provisioning
* Code analysis
* External system orchestration

> Core defines *what exists*, not *what happens*.

---

## Capabilities

Capabilities are independent subsystems that extend Core concepts with behavior.

Each capability:

* Depends on Core
* Has its own domain, persistence, and runtime
* Never redefines Core concepts

### Engine

**Purpose:** Infrastructure and lifecycle management

* Deployments
* Environments
* Resource templates
* Terraform provisioning & state

Owns concepts like:

* Resource
* Deployment
* Environment

---

### Inventory

**Purpose:** Discovery and ingestion of external systems

* Cloud resources
* Git repositories
* Pipelines
* Tickets / work items

Acts as the system of record for *observed* state.

---

### Analysis

**Purpose:** Structural and governance analysis of codebases

Examples:

* Framework and runtime versions across repositories
* Project structure analysis (e.g. csproj properties)
* Pipeline configuration analysis
* Repo hygiene and governance checks

Can run:

* Self‑hosted (local execution)
* Hosted (SaaS)

Uses Core entities like Repository, Application, and Organisation.

---

## Dependency Rules (Hard Constraints)

Allowed:

* Engine → Core
* Inventory → Core
* Analysis → Core
* Clients → APIs

Forbidden:

* Core → any capability
* Capability → capability direct calls

If coordination is required:

* Share state via Core
* Use events (future)

> If a sentence needs two owners, the architecture is leaking.

---

## Mental Model

```
          Inventory
              |
Analysis ─── Core ─── Engine
              |
        (future capabilities)
```

---

## Naming Conventions

* **Platform**: Conductor as a whole
* **Core**: Shared foundation
* **Capabilities**: Engine, Inventory, Analysis, etc.

This allows new products or capabilities to be added without renaming or restructuring the system.

---

## Guiding Principle

> Core stays small.
> Capabilities stay independent.
> The platform keeps growing.
