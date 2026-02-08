# Orchitect Architecture Summary

This document summarizes the architectural insights and direction discussed regarding the evolution of Orchitect and related tooling.

---

## Overview

Orchitect will consist of **three distinct but complementary products**, each addressing a different plane of a modern Internal Developer Platform (IDP):

1. **Orchitect.Platform** — Provisioning, orchestration, environment management
2. **Orchitect.Inventory** — Discovery and cataloging of existing infrastructure, repos, pipelines
3. **Orchitect.Intelligence** — Deep code insights, dependency analysis, modernization scoring

This separation creates a clean boundary between *control*, *visibility*, and *analysis*.

---

## Rationale

### Why Three Components?

Over time, multiple tools emerged:

- **CodeHub**: Fetched data from third-party providers (Azure, DevOps, GitHub, GitLab), but didn’t fit cleanly inside an IDP because it deals with *existing* infrastructure.
- **A repo and service analysis tool**: Cloned repos daily, scanned `.sln` / `.csproj`, analyzed frameworks, dependencies, and associated them with SonarCloud data, plus service ownership dashboards.
- **The Orchitect Engine**: A platform orchestrator for provisioning and managing new infrastructure.

Merging them into a single “IDP” didn’t make conceptual sense. They serve different purposes. Splitting them into three coherent products does.

---

## The Three Orchitect Products

### 1. **Orchitect.Platform**
**Purpose:**  
The core Internal Developer Platform engine. Provides:

- Infrastructure orchestration
- Environment and deployment management
- Configuration management
- RBAC and policy enforcement
- Application lifecycle automation

**Position:**  
This is the *Control Plane* of Orchitect.

---

### 2. **Orchitect.Inventory**
**Purpose:**  
A discovery and inventory system for **existing** infrastructure and code assets:

- Git repositories
- Cloud resources (Azure, AWS, GCP)
- Pipelines (Azure DevOps, GitHub Actions)
- SonarCloud projects
- Container registries
- Kubernetes workloads

**Why it’s separate:**  
Most IDPs do *not* manage legacy infra directly. Importing existing assets is outside typical provisioning lifecycles.  
This module acts as an ingestion and metadata plane, not a provisioning system.

**Position:**  
This is the *Visibility Plane* or *Catalog Plane*.

---

### 3. **Orchitect.Intelligence**
**Purpose:**  
A deeper analytics layer focused on codebases and technical quality:

- Repo scanning
- Framework and dependency detection
- Outdated package alerts
- Tech health scoring
- SonarCloud correlation
- Ownership insights and developer dashboards

**Position:**  
This is the *Insights Plane*, providing analysis rather than raw inventory or control.

---

## Naming Justification

The final names were chosen because they are:

- Clear and industry-aligned
- Flexible enough for future growth
- Representative of the product’s core responsibilities
- Cohesive as a family of products under the **Orchitect** brand

Final set:

