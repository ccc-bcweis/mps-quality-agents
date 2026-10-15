#!/usr/bin/env bash
# Copyright (c) 2026 Bryan C. Weis and Common Consulting Company
# Idempotent Entra setup for the demo sign-in app. Run once after the first deploy, signed in with az as an app owner.
# Usage: deploy/entra-setup.sh <signin-app-id> <container-app-fqdn>
set -euo pipefail
APP="$1"; FQDN="$2"
AZ_CLI=04b07795-8ddb-461a-bbee-02f9e1bf7b46
OID=$(az ad app show --id "$APP" --query id -o tsv)
az ad app update --id "$APP" --web-redirect-uris "https://$FQDN/.auth/login/aad/callback" --identifier-uris "api://$APP"
SID=$(az ad app show --id "$APP" --query "api.oauth2PermissionScopes[?value=='user_impersonation'].id | [0]" -o tsv)
if [ -z "$SID" ]; then
  SID=$(cat /proc/sys/kernel/random/uuid)
  az rest -m PATCH -u "https://graph.microsoft.com/v1.0/applications/$OID" --headers Content-Type=application/json \
    -b "{\"api\":{\"requestedAccessTokenVersion\":2,\"oauth2PermissionScopes\":[{\"id\":\"$SID\",\"value\":\"user_impersonation\",\"type\":\"User\",\"isEnabled\":true,\"adminConsentDisplayName\":\"Access MPS demo\",\"adminConsentDescription\":\"Access MPS demo\",\"userConsentDisplayName\":\"Access MPS demo\",\"userConsentDescription\":\"Access MPS demo\"}]}}"
fi
az rest -m PATCH -u "https://graph.microsoft.com/v1.0/applications/$OID" --headers Content-Type=application/json \
  -b "{\"api\":{\"preAuthorizedApplications\":[{\"appId\":\"$AZ_CLI\",\"delegatedPermissionIds\":[\"$SID\"]}]}}"
echo "Entra sign-in app $APP ready for https://$FQDN"
