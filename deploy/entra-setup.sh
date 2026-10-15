#!/usr/bin/env bash
# Copyright (c) 2026 Bryan C. Weis and Common Consulting Company
# Idempotent Entra setup for the demo sign-in app. Run after each deploy, signed in with az as an app owner.
# Usage: deploy/entra-setup.sh <signin-app-id> <container-app-fqdn> [admin-upn]
set -euo pipefail
APP="$1"; FQDN="$2"; ADMIN_UPN="${3:-}"
AZ_CLI=04b07795-8ddb-461a-bbee-02f9e1bf7b46
G=https://graph.microsoft.com/v1.0
OID=$(az ad app show --id "$APP" --query id -o tsv)
SP=$(az ad sp show --id "$APP" --query id -o tsv)
patch() { az rest -m PATCH -u "$G/applications/$OID" --headers Content-Type=application/json -b "$1" >/dev/null; }

# Easy Auth callback and identifier URI.
az ad app update --id "$APP" --web-redirect-uris "https://$FQDN/.auth/login/aad/callback" --identifier-uris "api://$APP"

# user_impersonation scope (created once), pre-authorized for the Azure CLI so assigned users can run deploy/smoke-live.sh.
SID=$(az ad app show --id "$APP" --query "api.oauth2PermissionScopes[?value=='user_impersonation'].id | [0]" -o tsv)
if [ -z "$SID" ]; then
  SID=$(cat /proc/sys/kernel/random/uuid)
  patch "{\"api\":{\"requestedAccessTokenVersion\":2,\"oauth2PermissionScopes\":[{\"id\":\"$SID\",\"value\":\"user_impersonation\",\"type\":\"User\",\"isEnabled\":true,\"adminConsentDisplayName\":\"Access MPS demo\",\"adminConsentDescription\":\"Access MPS demo\",\"userConsentDisplayName\":\"Access MPS demo\",\"userConsentDescription\":\"Access MPS demo\"}]}}"
fi
patch "{\"api\":{\"preAuthorizedApplications\":[{\"appId\":\"$AZ_CLI\",\"delegatedPermissionIds\":[\"$SID\"]}]}}"

# App role "admin" (demo reset). Created once.
RID=$(az ad app show --id "$APP" --query "appRoles[?value=='admin'].id | [0]" -o tsv)
if [ -z "$RID" ]; then
  RID=$(cat /proc/sys/kernel/random/uuid)
  patch "{\"appRoles\":[{\"id\":\"$RID\",\"value\":\"admin\",\"displayName\":\"Demo admin\",\"description\":\"Can reset the demo.\",\"allowedMemberTypes\":[\"User\"],\"isEnabled\":true}]}"
fi

# Assign the admin role to the named user (idempotent).
if [ -n "$ADMIN_UPN" ]; then
  UID_=$(az ad user show --id "$ADMIN_UPN" --query id -o tsv)
  HAS=$(az rest -m GET -u "$G/servicePrincipals/$SP/appRoleAssignedTo" --query "value[?principalId=='$UID_' && appRoleId=='$RID'] | length(@)" -o tsv)
  if [ "$HAS" = "0" ]; then
    az rest -m POST -u "$G/servicePrincipals/$SP/appRoleAssignedTo" --headers Content-Type=application/json \
      -b "{\"principalId\":\"$UID_\",\"resourceId\":\"$SP\",\"appRoleId\":\"$RID\"}" >/dev/null
  fi
fi
echo "Entra sign-in app $APP ready for https://$FQDN (admin role $RID)"
