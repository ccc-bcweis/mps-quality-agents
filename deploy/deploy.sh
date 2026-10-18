#!/usr/bin/env bash
# Copyright (c) 2026 Bryan C. Weis and Common Consulting Company
# Deploy the demo to YOUR Azure subscription from your machine, after `az login`. Same steps as .github/workflows/deploy.yml.
# Nothing here is tied to the original demo's tenant, subscription or vault.
#
# Required: RESOURCE_GROUP (created if missing, in LOCATION).
# Optional:
#   LOCATION         Azure region with Azure OpenAI quota (default eastus2).
#   NAME_PREFIX      Prefix for every resource. Registry and Key Vault names are global, so pick your own (default mpsdemo).
#   ENTRA_CLIENT_ID  Your Entra sign-in app (client) id. With ENTRA_ISSUER it turns on Easy Auth (sign-in mode azure).
#   ENTRA_ISSUER     For example https://login.microsoftonline.com/<your-tenant-id>/v2.0
#   ADMIN_EMAILS     Comma-separated admins. Default: the running app's list, else the signed-in az user (you).
#   BUDGET_EMAIL     Budget alert address. Blank skips the budget.
#   CUSTOM_DOMAIN          Optional host name. Blank means no custom domain.
#   CUSTOM_DOMAIN_CERT_ID  Optional full resource id of the managed certificate. Set with CUSTOM_DOMAIN, or leave both blank.
#   IMAGE_TAG        Default: the current git commit.
# The approval signing key is created in the new Key Vault on first deploy (random, never printed) if it is not there yet.
set -euo pipefail
cd "$(dirname "$0")/.."

: "${RESOURCE_GROUP:?Set RESOURCE_GROUP to your resource group name.}"
LOCATION="${LOCATION:-eastus2}"
NAME_PREFIX="${NAME_PREFIX:-mpsdemo}"
ENTRA_CLIENT_ID="${ENTRA_CLIENT_ID:-}"
ENTRA_ISSUER="${ENTRA_ISSUER:-}"
BUDGET_EMAIL="${BUDGET_EMAIL:-}"
CUSTOM_DOMAIN="${CUSTOM_DOMAIN:-}"
CUSTOM_DOMAIN_CERT_ID="${CUSTOM_DOMAIN_CERT_ID:-}"
IMAGE_TAG="${IMAGE_TAG:-$(git rev-parse --short=12 HEAD)}"
APP_NAME="${NAME_PREFIX}-api"
VAULT="${NAME_PREFIX}kv"
SECRET=approval-signing-key

az account show --query "{subscription:name, user:user.name}" -o table
az group create --name "$RESOURCE_GROUP" --location "$LOCATION" -o none

# Admins: explicit ADMIN_EMAILS, else the list on the running app, else the person running this script.
admins="${ADMIN_EMAILS:-}"
if [ -z "$admins" ]; then
  admins="$(az containerapp show -g "$RESOURCE_GROUP" -n "$APP_NAME" --query "properties.template.containers[0].env[?name=='Mps__Auth__AdminEmails'].value | [0]" -o tsv 2>/dev/null || true)"
fi
if [ -z "$admins" ] && [ "$(az account show --query user.type -o tsv)" = "user" ]; then
  admins="$(az account show --query user.name -o tsv)"
fi
if [ -n "$ENTRA_CLIENT_ID" ] && [ -z "$admins" ]; then
  echo "No admin email: set ADMIN_EMAILS. The app will not start in Entra mode without one." >&2
  exit 1
fi
if { [ -n "$CUSTOM_DOMAIN" ] && [ -z "$CUSTOM_DOMAIN_CERT_ID" ]; } || { [ -z "$CUSTOM_DOMAIN" ] && [ -n "$CUSTOM_DOMAIN_CERT_ID" ]; }; then
  echo "Set both CUSTOM_DOMAIN and CUSTOM_DOMAIN_CERT_ID, or leave both blank. One without the other stops the deploy so the custom domain binding is not dropped." >&2
  exit 1
fi
echo "Admins: ${admins:-(none, local sign-in mode)}"

deploy() { # $1 image, $2 approval key secret name ('' while the secret does not exist yet)
  az deployment group create \
    --resource-group "$RESOURCE_GROUP" \
    --name mps-demo \
    --template-file infra/main.bicep \
    --parameters infra/main.bicepparam \
    --parameters \
      location="$LOCATION" \
      namePrefix="$NAME_PREFIX" \
      apiImage="$1" \
      approvalKeySecretName="$2" \
      entraClientId="$ENTRA_CLIENT_ID" \
      openIdIssuer="$ENTRA_ISSUER" \
      adminEmails="$admins" \
      budgetContactEmail="$BUDGET_EMAIL" \
      customDomainName="$CUSTOM_DOMAIN" \
      customDomainCertificateId="$CUSTOM_DOMAIN_CERT_ID" \
      deploySql=false \
      deployStaticWeb=false \
    -o none
}
output() { az deployment group show -g "$RESOURCE_GROUP" -n mps-demo --query "properties.outputs.$1.value" -o tsv; }

key_name=""
if az keyvault secret show --vault-name "$VAULT" --name "$SECRET" --query id -o none 2>/dev/null; then key_name="$SECRET"; fi
echo "1/4 Bootstrap (registry, vault, identity, OpenAI, app shell)"
deploy mcr.microsoft.com/dotnet/aspnet:10.0 "$key_name"

if [ -z "$key_name" ]; then
  echo "2/4 Creating the approval signing key in $VAULT (random 32 bytes, not printed)"
  me="$(az ad signed-in-user show --query id -o tsv 2>/dev/null || true)"
  if [ -n "$me" ]; then
    az role assignment create --assignee-object-id "$me" --assignee-principal-type User --role "Key Vault Secrets Officer" \
      --scope "$(az keyvault show -n "$VAULT" --query id -o tsv)" -o none 2>/dev/null || true
    sleep 30
  fi
  tmp="$(mktemp)"; trap 'rm -f "$tmp"' EXIT
  openssl rand -hex 32 | tr -d '\n' > "$tmp"
  az keyvault secret set --vault-name "$VAULT" --name "$SECRET" --file "$tmp" -o none
  rm -f "$tmp"
else
  echo "2/4 Approval signing key already in $VAULT"
fi

echo "3/4 Building the image in the registry"
acr="$(output acrName)"
az acr build --registry "$acr" --image "mps-app:${IMAGE_TAG}" --file deploy/app.Dockerfile . -o none

echo "4/4 Deploying the app"
deploy "$(output acrLoginServer)/mps-app:${IMAGE_TAG}" "$SECRET"
fqdn="$(output appFqdn)"
echo "Demo URL: https://${fqdn}  (health: https://${fqdn}/health)"
if [ -n "$ENTRA_CLIENT_ID" ]; then
  echo "Next: deploy/entra-setup.sh $ENTRA_CLIENT_ID $fqdn  (sets the sign-in redirect URI on your Entra app)"
fi
