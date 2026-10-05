#!/usr/bin/env bash
# Makes a registered user a member of every organisation that has no members.

set -euo pipefail

usage() {
    cat >&2 <<EOF
Usage: $0 <connection-string> <user-email> [--dry-run]

  connection-string  libpq connection string or URI for the Orchitect database,
                     e.g. postgresql://postgres:<password>@localhost:41031/orchitect
  user-email         email of the registered user to add
  --dry-run          list the organisations that would get the user, without changing anything

Set PSQL to run psql another way, e.g. PSQL="docker exec -i <postgres-container> psql".
EOF
    exit 1
}

[[ $# -lt 2 || $# -gt 3 ]] && usage

CONNECTION_STRING="$1"
EMAIL="$2"
DRY_RUN=false

if [[ $# -eq 3 ]]; then
    [[ "$3" == "--dry-run" ]] || usage
    DRY_RUN=true
fi

read -r -a PSQL_COMMAND <<< "${PSQL:-psql}"

run_sql() {
    "${PSQL_COMMAND[@]}" "$CONNECTION_STRING" -X -q -t -A -v ON_ERROR_STOP=1 -v email="$EMAIL"
}

USER_COUNT=$(run_sql <<'SQL'
SELECT count(*) FROM "Users" WHERE "NormalizedEmail" = upper(:'email');
SQL
)

if [[ "$USER_COUNT" != "1" ]]; then
    echo "No user is registered with the email '$EMAIL'." >&2
    exit 1
fi

if [[ "$DRY_RUN" == true ]]; then
    echo "Organisations without members:"
    run_sql <<'SQL'
SELECT o."Id" || ' ' || o."Name"
FROM "Organisations" o
WHERE NOT EXISTS (SELECT 1 FROM "OrganisationUsers" m WHERE m."OrganisationId" = o."Id")
ORDER BY o."Name";
SQL
    exit 0
fi

echo "Added '$EMAIL' to:"
run_sql <<'SQL'
WITH added AS (
    INSERT INTO "OrganisationUsers" ("Id", "IdentityUserId", "OrganisationId")
    SELECT gen_random_uuid(), u."Id", o."Id"
    FROM "Organisations" o
    JOIN "Users" u ON u."NormalizedEmail" = upper(:'email')
    WHERE NOT EXISTS (SELECT 1 FROM "OrganisationUsers" m WHERE m."OrganisationId" = o."Id")
    RETURNING "OrganisationId"
)
SELECT o."Id" || ' ' || o."Name"
FROM added a
JOIN "Organisations" o ON o."Id" = a."OrganisationId"
ORDER BY o."Name";
SQL
