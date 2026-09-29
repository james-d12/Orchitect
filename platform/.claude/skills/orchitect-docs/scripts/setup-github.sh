#!/usr/bin/env bash
# Idempotently creates the labels and milestones described in references/conventions.md.
set -euo pipefail

REPO="${REPO:-james-d12/Orchitect}"

labels=(
  "area:runner|1d76db|Runner container / executor"
  "area:engine|0e8a16|Engine provisioning"
  "area:resource|5319e7|Resource domain and deployments"
  "area:inventory|fbca04|Inventory discovery"
  "area:auth|b60205|Authentication, authorisation, secrets"
  "area:docs|c5def5|Documentation and architecture"
  "type:feature|a2eeef|New capability"
  "type:bug|d73a4a|Incorrect or unsafe behaviour"
  "type:tech-debt|cfd3d7|Refactor or cleanup"
  "type:test|bfd4f2|Tests or verification"
  "type:decision|f9d0c4|Needs a recorded decision"
  "type:docs|0075ca|Documentation change"
  "size:S|ededed|Under a day"
  "size:M|d4c5f9|A few days"
  "size:L|7057ff|A week or more / needs splitting"
)

milestones=(
  "Runner Isolation|Runner container isolation, deployment runs and the API/runner contract. Docs: platform/docs/runner/"
  "Resource Domain|Resource aggregates, Phase 2 requirements/bindings/deltas, credentials for the Engine. Docs: platform/docs/resource/"
  "Inventory Discovery|Discovery configuration, scheduling and persisted inventory data. Docs: platform/docs/inventory/"
  "Security & Auth|Authentication, authorisation and tenant isolation. Docs: platform/docs/architecture/"
  "Docs & Architecture|Architecture docs, CLAUDE.md and architecture tests. Docs: platform/docs/architecture/"
)

for entry in "${labels[@]}"; do
  IFS='|' read -r name color desc <<<"$entry"
  gh label create "$name" --color "$color" --description "$desc" --force -R "$REPO" >/dev/null
  echo "label: $name"
done

existing="$(gh api "repos/$REPO/milestones?state=all&per_page=100" --jq '.[].title')"
for entry in "${milestones[@]}"; do
  IFS='|' read -r title desc <<<"$entry"
  if grep -qxF "$title" <<<"$existing"; then
    echo "milestone exists: $title"
  else
    gh api "repos/$REPO/milestones" -f title="$title" -f description="$desc" >/dev/null
    echo "milestone created: $title"
  fi
done
