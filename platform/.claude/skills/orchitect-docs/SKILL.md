---
name: orchitect-docs
description: Read, create, update or sync Orchitect design docs in platform/docs together with their GitHub issues and milestones on james-d12/Orchitect. Use when asked about the status of a plan/design doc, to write a new design doc, to mark work in a doc as done, to raise issues for outstanding work, or to check docs and issues are consistent.
user-invocable: true
allowed-tools:
  - Read
  - Edit
  - Write
  - Glob
  - Grep
  - Bash(gh *)
  - Bash(git mv *)
  - Bash(git log *)
  - Bash(bash *check-docs.sh*)
---

# /orchitect-docs — Keep docs and GitHub issues in sync

Arguments passed: `$ARGUMENTS`

The first word is the subcommand: `read`, `create`, `update` or `sync`. The rest is the topic or doc path. If `$ARGUMENTS` is empty, ask which subcommand the user wants.

Before doing anything, read `references/conventions.md` (in this skill folder). It defines:
- the folder layout
- the front-matter schema and statuses
- the milestone ↔ workstream map
- the labels
- the issue body template

Paths below are relative to `platform/`. Every `gh` command passes `-R james-d12/Orchitect`.

**Core rule:** issues hold the work, docs hold the design.
- Don't keep a separate to-do list in a doc that isn't linked to issues.
- Any outstanding item in a doc must point to an issue (`#NN`).

**Confirmation rule:** creating, closing or editing GitHub issues is visible to others.
1. Always show the exact list first: titles, milestone, labels, and close reasons.
2. Wait for the user's confirmation before calling `gh issue create`, `gh issue close` or `gh issue edit`.

---

## `read <topic>`

1. Open `docs/README.md` and find matching docs. If nothing obvious matches, grep the front matter (`title:`, `workstream:`) under `docs/`.
2. Read the doc. Report:
   - its front matter (status, milestone, last reviewed)
   - a short summary of the design
3. Get the state of the doc's issues with `gh issue view <n> -R james-d12/Orchitect --json number,title,state,labels`. For many issues, use `gh issue list -R james-d12/Orchitect --milestone "<milestone>" --state all --limit 200 --json number,title,state`. Report open vs closed.
4. Flag drift you notice:
   - a closed issue whose item the doc still describes as outstanding
   - `last_reviewed` older than about 60 days while the doc is `active`

## `create <topic>`

1. Choose the workstream and folder from the conventions. If the workstream is unclear, ask.
2. Copy `templates/doc.md` to `docs/<folder>/<NAME>.md`. Fill in the front matter:
   - `status: active`
   - `issues: []`
   - `last_reviewed:` today's date
3. Write the design with the user. List outstanding work under **Outstanding** as short items; they don't have issue numbers yet.
4. Draft one issue per item, using the conventions' template, milestone and labels. Before creating any issue:
   - Check for duplicates with `gh issue list -R james-d12/Orchitect --state all --search "in:title <key words>"`.
   - Show the full list and get confirmation.
5. Create the issues with `gh issue create -R james-d12/Orchitect --title … --body-file … --milestone … --label …`.
6. Write the numbers back:
   - into each Outstanding item (`- … (#NN)`)
   - into front-matter `issues: [..]`
7. Add a row to the right table in `docs/README.md`.

## `update <doc>`

1. Read the doc and its issues, as in `read`.
2. Check each outstanding item against the code (Grep/Read under `src/`) and `git log --oneline -- <paths>`. Put each item in one of three states:
   - done
   - still open
   - no longer relevant
3. Propose changes and apply them once confirmed:
   - Done items: tick or strike them in the doc and add the commit hash. Close the issue with `gh issue close <n> -R james-d12/Orchitect --comment "Done in <sha>"`.
   - No longer relevant: close the issue as `--reason "not planned"` with an explanation.
   - New work found: create issues (dedupe first) and link them.
4. Set `last_reviewed` to today.
5. If no open issues remain, finish the doc:
   - Set `status: done` (or `superseded` and `superseded_by: <path>`).
   - Add the one-line banner under the front matter.
   - `git mv` it into `docs/archive/`.
   - Move its row in `docs/README.md` to the Archive table.
   - Grep for links to the old path and fix them.

## `sync`

1. Run `bash .claude/skills/orchitect-docs/scripts/check-docs.sh` from `platform/`.
2. It reports:
   - missing or invalid front matter
   - docs not listed in the README
   - issue numbers that don't exist
   - closed issues still listed on `active` docs
   - open milestone issues that no doc references
3. For each finding, propose the fix: edit the front matter or README, link the orphan issue to the right doc, or run `update` on the doc. Apply the fixes once confirmed.
