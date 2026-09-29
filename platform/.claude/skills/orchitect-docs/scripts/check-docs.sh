#!/usr/bin/env bash
# Reports drift between platform/docs front matter, docs/README.md and GitHub issues.
set -uo pipefail

REPO="james-d12/Orchitect"
DOCS_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../../../docs" && pwd)"
README="$DOCS_DIR/README.md"
problems=0

report() {
  local message="$1"
  echo "  - $message"
  problems=$((problems + 1))
  return 0
}

field() {
  local file="$1" key="$2"
  awk -v k="$key" 'NR==1&&$0!="---"{exit} NR>1&&$0=="---"{exit} NR>1{ if (index($0, k":")==1) { sub(k":[ ]*",""); print; exit } }' "$file"
  return $?
}

declare -A doc_issues doc_status
echo "Front matter:"
while IFS= read -r -d '' file; do
  rel="${file#"$DOCS_DIR"/}"
  [[ "$rel" == "README.md" ]] && continue
  if [[ "$(head -1 "$file")" != "---" ]]; then report "$rel: no front matter"; continue; fi
  for key in title status workstream milestone issues last_reviewed; do
    [[ -z "$(field "$file" "$key")" ]] && report "$rel: missing '$key'"
  done
  status="$(field "$file" status | sed 's/[[:space:]]*#.*//')"
  doc_status["$rel"]="$status"
  case "$status" in
    active|reference) [[ "$rel" == archive/* ]] && report "$rel: status '$status' but lives in archive/" ;;
    done|superseded) [[ "$rel" != archive/* ]] && report "$rel: status '$status' but not in archive/" ;;
    *) report "$rel: invalid status '$status'" ;;
  esac
  if [[ "$status" == "superseded" ]]; then
    sup="$(field "$file" superseded_by)"
    [[ "$sup" == "null" || -z "$sup" ]] && report "$rel: superseded but no superseded_by"
    [[ "$sup" != "null" && -n "$sup" && ! -e "$DOCS_DIR/$sup" ]] && report "$rel: superseded_by '$sup' does not exist"
  fi
  doc_issues["$rel"]="$(field "$file" issues | tr -d '[] ' | tr ',' ' ')"
done < <(find "$DOCS_DIR" -name '*.md' -print0 | sort -z)

echo "README index:"
for rel in "${!doc_status[@]}"; do
  grep -qF "($rel)" "$README" || report "$rel: not listed in docs/README.md"
done
while IFS= read -r link; do
  target="${link//%20/ }"
  [[ -e "$DOCS_DIR/$target" ]] || report "README links to missing '$link'"
done < <(grep -oE '\]\([^)#]+\)' "$README" | sed -E 's/^\]\(//; s/\)$//' | grep -v '^http')

echo "GitHub ($REPO):"
if ! command -v gh >/dev/null 2>&1; then
  echo "  (gh not installed; skipping issue checks)"
else
  issues_json="$(gh issue list -R "$REPO" --state all --limit 1000 --json number,state,milestone,title 2>/dev/null)" \
    || { echo "  (gh issue list failed; skipping issue checks)"; issues_json=""; }
  if [[ -n "$issues_json" ]]; then
    declare -A state
    while IFS=$'\t' read -r n s; do state["$n"]="$s"; done \
      < <(jq -r '.[] | "\(.number)\t\(.state)"' <<<"$issues_json")
    declare -A referenced
    for rel in "${!doc_issues[@]}"; do
      for n in ${doc_issues[$rel]}; do
        referenced["$n"]=1
        if [[ -z "${state[$n]:-}" ]]; then report "$rel: issue #$n does not exist"
        elif [[ "${state[$n]}" == "CLOSED" && "${doc_status[$rel]}" == "active" ]]; then
          grep -qE "~~.*#$n\b|\[x\].*#$n\b|Done.*#$n\b" "$DOCS_DIR/$rel" \
            || report "$rel: issue #$n is closed but the doc is active and doesn't mark it done"
        fi
      done
    done
    while IFS=$'\t' read -r n t; do
      [[ -z "${referenced[$n]:-}" ]] && report "open issue #$n '$t' is not referenced by any doc"
    done < <(jq -r '.[] | select(.state=="OPEN" and .milestone != null) | "\(.number)\t\(.title)"' <<<"$issues_json")
  fi
fi

echo
if (( problems == 0 )); then echo "OK: no drift found."; else echo "$problems problem(s) found."; exit 1; fi
