# Orchitect docs conventions

## Folder layout (`platform/docs/`)

| Folder | Holds |
|---|---|
| `README.md` | Index: every doc with its status, workstream and summary |
| `runner/` | Runner / executor / deployment-run design |
| `resource/` | Resource domain, deployments, credentials used by the Engine |
| `inventory/` | Discovery and inventory design (create when first needed) |
| `architecture/` | Cross-cutting architecture and security reference |
| `archive/` | Docs with status `done` or `superseded` |

File names: `UPPER_SNAKE_CASE.md` for plans and designs, `kebab-case.md` for reference notes. Move docs with `git mv` so their history is kept.

## Front matter (required on every `.md` except `README.md`)

```yaml
---
title: "Human readable title"
status: active            # active | reference | done | superseded
workstream: runner        # runner | resource | inventory | security | architecture
milestone: "Runner Isolation"
issues: [12, 13]          # every GitHub issue this doc owns
superseded_by: null       # docs/-relative path when status is superseded
last_reviewed: 2026-09-29 # ISO date of the last check against the code
---
```

**Statuses:**
- `active`: has open work. Every outstanding item links to an open issue.
- `reference`: background material. `issues` may be empty.
- `done`: all its issues are closed, and the doc lives in `archive/`. It gets a one-line banner under the front matter: `> **Done: <what landed, commit hashes>. <where leftovers are tracked>.**`
- `superseded`: replaced. `superseded_by` is set, the doc lives in `archive/`, and it gets the same kind of banner.

## Workstream ↔ milestone

| workstream | milestone | default area label |
|---|---|---|
| runner | Runner Isolation | `area:runner` |
| resource | Resource Domain | `area:resource` |
| inventory | Inventory Discovery | `area:inventory` |
| security | Security & Auth | `area:auth` |
| architecture | Docs & Architecture | `area:docs` |

If a new workstream is needed, create its milestone first (`gh api repos/james-d12/Orchitect/milestones -f title="…"`), then add it here and to `docs/README.md`.

## Labels

- **Area:** `area:runner`, `area:engine`, `area:resource`, `area:inventory`, `area:auth`, `area:docs`
- **Type:** `type:feature`, `type:bug`, `type:tech-debt`, `type:test`, `type:decision`, `type:docs`
- **Size:**
  - `size:S`: under a day
  - `size:M`: a few days
  - `size:L`: a week or more, or needs splitting

Each issue gets one type, one size, and one or more area labels.

## Issue template

Title: imperative and specific, e.g. "Add `POST /deployments/{id}/cancel`". Don't put IDs like "H1" in the title.

```markdown
## Summary
<1–3 sentences: the problem and what to do about it>

## Source
- `platform/docs/<path>.md` — <section / finding ID>

## Acceptance criteria
- [ ] <observable outcome>
- [ ] <tests / docs updated>

## Dependencies
- Blocked by #NN / Related to #NN   (omit section if none)
```

## Rules
- Check for duplicates before creating an issue: `gh issue list -R james-d12/Orchitect --state all --search "in:title <key words>"`.
- One issue per independently shippable item. Split anything that would be `size:L` and has clear parts.
- Decisions get a `type:decision` issue. The decision is recorded in the doc, and the issue is closed with a link to it.
- Docs never hold untracked to-dos. If something is outstanding, it has an issue number next to it.
- Show and confirm before creating, closing or editing issues.
