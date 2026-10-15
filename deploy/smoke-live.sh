#!/usr/bin/env bash
# Copyright (c) 2026 Bryan C. Weis and Common Consulting Company
# End-to-end smoke against the hosted demo as the signed-in az user (must be assigned to the sign-in app).
# Usage: deploy/smoke-live.sh <signin-app-id> <container-app-fqdn>
set -euo pipefail
APP="$1"; F="https://$2"
T=$(az account get-access-token --scope "api://$APP/user_impersonation" --query accessToken -o tsv)
c() { curl -sf -H "Authorization: Bearer $T" -H "Content-Type: application/json" "$@"; }
c -X POST "$F/api/faults" -d '{"toolId":"D-01","chamberId":"B","parameter":"chamberPressure","shape":"step","magnitude":4.5,"rootCauseCategory":"EquipmentCalibrationOrAlignment"}' >/dev/null
sleep 2
echo "alert:  $(c "$F/api/alerts?toolId=D-01" | jq -c '.[0] | {rule, severity, parameter}')"
echo "ticket: $(c "$F/api/tickets" | jq -r '.[0].number')"
INC=$(c "$F/api/incidents" | jq -r '[.[] | select(.stage != "closed" and .stage != "approved")][0].id')
INV=$(c -X POST "$F/api/incidents/$INC/investigations")
echo "investigation: $(echo "$INV" | jq -c '{stage, evidence: (.evidence | length), hypotheses: [.hypotheses[]? | {category, confidence}], escalationReason}')"
ID=$(echo "$INV" | jq -r .id)
echo "decision: $(c -X POST "$F/api/investigations/$ID/decisions" -d '{"kind":"Approve","comment":"Smoke test","approverId":"smoke"}' | jq -c '{stage: .investigation.stage, capa: .capa.id}')"
echo "usage:  $(c "$F/api/model/usage" | jq -c '{spendTodayUsd, byRoute}')"
