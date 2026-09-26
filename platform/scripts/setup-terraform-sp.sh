#!/usr/bin/env bash
# Creates or reuses the terraform service principal and stores its credentials in Key Vault as terraform-*.

set -euo pipefail

usage() {
    echo "Usage: $0 <app-name> <subscription-id> <keyvault-name> [--role <role>]" >&2
    exit 1
}

[[ $# -lt 3 ]] && usage

APP_NAME="$1"
SUBSCRIPTION_ID="$2"
VAULT_NAME="$3"
shift 3

ROLE="Contributor"

while [[ $# -gt 0 ]]; do
    case "$1" in
        --role) ROLE="${2:?--role needs a value}"; shift 2 ;;
        *) usage ;;
    esac
done

log() { echo "==> $*"; }

command -v az >/dev/null || { echo "az CLI is required." >&2; exit 1; }
az account show >/dev/null 2>&1 || { echo "Run 'az login' first." >&2; exit 1; }

az account set --subscription "$SUBSCRIPTION_ID"
TENANT_ID="$(az account show --query tenantId -o tsv)"
SCOPE="/subscriptions/$SUBSCRIPTION_ID"

VAULT_ID="$(az keyvault show --name "$VAULT_NAME" --query id -o tsv)"
VAULT_URI="$(az keyvault show --name "$VAULT_NAME" --query properties.vaultUri -o tsv)"
VAULT_RBAC="$(az keyvault show --name "$VAULT_NAME" --query properties.enableRbacAuthorization -o tsv)"
USER_OBJECT_ID="$(az ad signed-in-user show --query id -o tsv)"

if [[ "$VAULT_RBAC" == "true" ]]; then
    VAULT_ROLE="Key Vault Secrets Officer"
    EXISTING="$(az role assignment list --assignee "$USER_OBJECT_ID" --role "$VAULT_ROLE" --scope "$VAULT_ID" \
        --query "length(@)" -o tsv)"

    if [[ "$EXISTING" == "0" ]]; then
        log "Granting signed-in user '$VAULT_ROLE' on $VAULT_NAME"
        az role assignment create --assignee-object-id "$USER_OBJECT_ID" --assignee-principal-type User \
            --role "$VAULT_ROLE" --scope "$VAULT_ID" -o none
    else
        log "Signed-in user already has '$VAULT_ROLE' on $VAULT_NAME"
    fi
else
    log "Granting signed-in user secret get/list/set access policy on $VAULT_NAME"
    az keyvault set-policy --name "$VAULT_NAME" --object-id "$USER_OBJECT_ID" \
        --secret-permissions get list set -o none
fi

APP_ID="$(az ad app list --display-name "$APP_NAME" --query "[0].appId" -o tsv)"

if [[ -z "$APP_ID" ]]; then
    log "Creating app registration '$APP_NAME'"
    APP_ID="$(az ad app create --display-name "$APP_NAME" --query appId -o tsv)"
else
    log "Using existing app registration '$APP_NAME' ($APP_ID)"
fi

SP_OBJECT_ID="$(az ad sp show --id "$APP_ID" --query id -o tsv 2>/dev/null || true)"

if [[ -z "$SP_OBJECT_ID" ]]; then
    log "Creating service principal for $APP_ID"
    SP_OBJECT_ID="$(az ad sp create --id "$APP_ID" --query id -o tsv)"
fi

EXISTING="$(az role assignment list --assignee "$SP_OBJECT_ID" --role "$ROLE" --scope "$SCOPE" \
    --query "length(@)" -o tsv)"

if [[ "$EXISTING" == "0" ]]; then
    log "Assigning '$ROLE' on $SCOPE"
    az role assignment create --assignee-object-id "$SP_OBJECT_ID" --assignee-principal-type ServicePrincipal \
        --role "$ROLE" --scope "$SCOPE" -o none
else
    log "Service principal already has '$ROLE' on $SCOPE"
fi

log "Creating new client secret"
CLIENT_SECRET="$(az ad app credential reset --id "$APP_ID" --append --display-name orchitect-terraform \
    --years 1 --query password -o tsv 2>/dev/null)"

set_secret() {
    local name="$1" value="$2" attempt

    for attempt in 1 2 3 4 5 6; do
        if printf '%s' "$value" | az keyvault secret set --vault-name "$VAULT_NAME" --name "$name" \
            --file /dev/stdin --encoding utf-8 -o none 2>/dev/null; then
            log "Stored $name"
            return 0
        fi

        log "Writing $name failed (attempt $attempt), waiting for Key Vault access to propagate"
        sleep 20
    done

    echo "Could not write $name to $VAULT_NAME." >&2
    exit 1
}

set_secret terraform-client-id "$APP_ID"
set_secret terraform-client-secret "$CLIENT_SECRET"
set_secret terraform-tenant-id "$TENANT_ID"
set_secret terraform-subscription-id "$SUBSCRIPTION_ID"

unset CLIENT_SECRET

cat <<EOF

Done.
  App registration : $APP_NAME ($APP_ID)
  Role             : $ROLE on $SCOPE
  Key Vault        : $VAULT_URI
  Secrets          : terraform-client-id, terraform-client-secret, terraform-tenant-id, terraform-subscription-id

Set the AppHost keyvault-uri parameter:
  dotnet user-secrets --project src/Orchitect.AppHost set "Parameters:keyvault-uri" "$VAULT_URI"
EOF
