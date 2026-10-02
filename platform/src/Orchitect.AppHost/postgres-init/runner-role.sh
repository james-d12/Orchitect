#!/bin/sh
set -e

psql -v ON_ERROR_STOP=1 -v password="$ORCHITECT_RUNNER_PASSWORD" --username "$POSTGRES_USER" --dbname postgres <<'EOSQL'
CREATE ROLE orchitect_runner LOGIN PASSWORD :'password';
EOSQL
