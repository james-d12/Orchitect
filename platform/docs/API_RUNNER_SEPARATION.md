# Orchitect Runner Architecture

## 1. Purpose

Refactor `Orchitect.Runner` into a lightweight, disposable execution worker.

The Runner should **not have direct access to the Orchitect database**. Instead, it communicates with the Orchitect API over an authenticated internal API.

The Orchitect API remains responsible for:

* Persistent state
* Run lifecycle
* Score configuration
* Resource/template resolution
* Environment configuration
* Runner coordination
* Recording execution results

The Runner is responsible for:

* Obtaining its execution context
* Executing the requested orchestration
* Invoking Terraform, Helm, cloud providers, etc.
* Reporting progress and results
* Returning a meaningful exit status

---

# 2. Architecture

```text
                         ┌──────────────────────────┐
                         │      Orchitect API       │
                         │                          │
                         │  Application             │
                         │  Orchestration           │
                         │  Persistence             │
                         │  Resource Templates     │
                         │  Score Configuration    │
                         └────────────┬─────────────┘
                                      │
                              Internal API
                           authenticated request
                                      │
                                      ▼
                         ┌──────────────────────────┐
                         │    Orchitect Runner      │
                         │                          │
                         │  Execution Context       │
                         │  Orchestration Executor  │
                         │  Provisioners            │
                         │  Terraform               │
                         │  Helm                    │
                         │  Cloud Provider          │
                         └──────────────────────────┘
                                      │
                                      ▼
                              Infrastructure
```

The Runner is therefore a worker attached to a specific Orchitect run.

---

# 3. Core Responsibility Split

## Orchitect API

The API owns the control plane.

```text
API
├── Run lifecycle
├── Database
├── Score
├── Resource definitions
├── Resource templates
├── Environment configuration
├── Secrets/configuration resolution
├── Runner lifecycle
└── Execution state
```

It answers the question:

> What should this run execute?

## Runner

The Runner owns execution.

```text
Runner
├── Obtain execution context
├── Validate execution context
├── Execute orchestration
├── Invoke provisioners
├── Capture output
├── Report status
└── Return exit code
```

It answers:

> How do I execute what I have been asked to execute?

---

# 4. Runner Lifecycle

A Runner should have a simple lifecycle.

```text
                 Runner created
                       │
                       ▼
                Authenticate
                       │
                       ▼
              Initialise Run
                       │
                       ▼
             Obtain Run Context
                       │
                       ▼
             Validate Context
                       │
                       ▼
                 Execute
                       │
              ┌────────┴────────┐
```
