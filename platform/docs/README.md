# Orchitect platform docs

GitHub issues hold the work; these docs hold the design and the reasoning behind it. Each doc has YAML front matter (`status`, `workstream`, `milestone`, `issues`, `last_reviewed`). Use the `/orchitect-docs` skill (`platform/.claude/skills/orchitect-docs/`) to read, create or update docs, so that front matter, this index and the GitHub issues stay in sync.

**Statuses:**
- `active`: design with open work.
- `reference`: background material, with no work tracked against it.
- `done`: finished, kept in `archive/` as a record.
- `superseded`: replaced by another doc and kept in `archive/`.

## Workstreams

| Workstream | Milestone | Folder |
|---|---|---|
| runner | [Runner Isolation](https://github.com/james-d12/Orchitect/milestone/1) | `runner/` |
| resource | [Resource Domain](https://github.com/james-d12/Orchitect/milestone/2) | `resource/` |
| inventory | [Inventory Discovery](https://github.com/james-d12/Orchitect/milestone/3) | `inventory/` (no active docs yet) |
| security | [Security & Auth](https://github.com/james-d12/Orchitect/milestone/4) | `architecture/` |
| architecture | [Docs & Architecture](https://github.com/james-d12/Orchitect/milestone/5) | `architecture/` |

## Active and reference docs

| Doc | Status | Workstream | Summary |
|---|---|---|---|
| [runner/RUNNER_TODO.md](runner/RUNNER_TODO.md) | active | runner | Runner operator guide (config, build) plus the remaining checklist |
| [runner/API_RUNNER_SEPARATION.md](runner/API_RUNNER_SEPARATION.md) | active | runner | Target design: thin runner that calls an internal run API (auth, contract, status, secrets, migration order) |
| [runner/Runner_Isolation_Branch_Review.md](runner/Runner_Isolation_Branch_Review.md) | active | runner | Code review E1–E15, M1–M10, H1–H4 (E/M done, H open) |
| [runner/Runner_Isolation_Branch_Review_Architecture.md](runner/Runner_Isolation_Branch_Review_Architecture.md) | active | runner | Architecture review A1–A10 |
| [resource/RESOURCE_DOMAIN_REFACTOR_PLAN_PHASE_2.md](resource/RESOURCE_DOMAIN_REFACTOR_PLAN_PHASE_2.md) | active | resource | Requirements, bindings, deltas and snapshots (not started; Step 3 needs a rethink) |
| [resource/resource-workflows.md](resource/resource-workflows.md) | reference | resource | Resource lifecycle workflows and state diagram |
| [architecture/HIGH_LEVEL_ARCHITECTURE.md](architecture/HIGH_LEVEL_ARCHITECTURE.md) | reference | architecture | Core plus capabilities, and the dependency rules |
| [architecture/High Level Diagram.drawio](architecture/High%20Level%20Diagram.drawio) | reference | architecture | Architecture diagram |
| [architecture/JWT_BEST_PRACTICES.md](architecture/JWT_BEST_PRACTICES.md) | reference | security | JWT / RFC 8725 guidance |

## Archive

| Doc | Status | Replaced by / notes |
|---|---|---|
| [archive/Runner_Isolation_Fix_Plan.md](archive/Runner_Isolation_Fix_Plan.md) | done | E1–E15, M1–M9 fixed; M10 dropped |
| [archive/REVIEW_CODE.md](archive/REVIEW_CODE.md) | done | Prompt behind the `runner/` reviews |
| [archive/PROVISIONING_ISOLATION_ARCHITECTURE.md](archive/PROVISIONING_ISOLATION_ARCHITECTURE.md) | superseded | `runner/API_RUNNER_SEPARATION.md` |
| [archive/RESOURCE_DOMAIN_REFACTOR_PLAN.md](archive/RESOURCE_DOMAIN_REFACTOR_PLAN.md) | done | Phase 1, `fdbdb89` |
| [archive/RESOURCE_DOMAIN_PROVISIONING_PHASE_1_OUTSTANDING.md](archive/RESOURCE_DOMAIN_PROVISIONING_PHASE_1_OUTSTANDING.md) | done | Phase 1 persistence |
| [archive/INFRASTRUCTURE_STATE_MANAGEMENT.md](archive/INFRASTRUCTURE_STATE_MANAGEMENT.md) | superseded | `resource/RESOURCE_DOMAIN_REFACTOR_PLAN_PHASE_2.md` |
| [archive/INFRA_PLAN.md](archive/INFRA_PLAN.md) | superseded | `resource/RESOURCE_DOMAIN_REFACTOR_PLAN_PHASE_2.md` |
| [archive/resource-models.md](archive/resource-models.md) | superseded | `resource/resource-workflows.md` |
| [archive/MERGING.md](archive/MERGING.md) | superseded | `architecture/HIGH_LEVEL_ARCHITECTURE.md` |
| [archive/INVENTORY_INTEGRATION_CONFIG_PLAN.md](archive/INVENTORY_INTEGRATION_CONFIG_PLAN.md) | superseded | `archive/DISCOVERY_CREDENTIAL_INTEGRATION_PLAN.md` |
| [archive/DISCOVERY_CREDENTIAL_INTEGRATION_PLAN.md](archive/DISCOVERY_CREDENTIAL_INTEGRATION_PLAN.md) | done | Per-organisation discovery configs |
| [archive/INVENTORY_SAVEDATA_DB.md](archive/INVENTORY_SAVEDATA_DB.md) | done | Discovered data stored in the DB |
| [archive/CREDENTIAL_FEATURE_PLAN.md](archive/CREDENTIAL_FEATURE_PLAN.md) | done | Core credentials (Engine usage in issues) |

`humanitec-openapi.json` is git-ignored local reference material.
