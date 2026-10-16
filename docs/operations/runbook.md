# Operations runbook (hosted demo)

Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

Who uses this: the person running the hosted demo in `rg-mps-demo`. Times are the presenter's local time. Every company, plant, and record in the app is fictional.

## Deploy

1. Merge to `main` with CI green (`ci.yml`: build, format, tests including `Api.IntegrationTests`, the replay eval, web lint/test/build).
2. Run `gh workflow run deploy.yml --ref main` (or Actions > deploy > Run workflow).
3. The `azure-demo` environment needs a required reviewer. Approve the pending deployment.
4. The job federates to Azure with OIDC (no stored secrets), pushes the image to ACR, and rolls a new Container Apps revision.
5. **Every deploy wipes the demo data.** The app starts from seed 4242 with a healthy week of history.

## Smoke check after deploy (5 minutes)

| Check | Expected |
|---|---|
| Sign in as an invited guest | Console loads; uninvited accounts get no token |
| Harness bar | AI engine badge and spend meter visible |
| `GET /api/metrics` | `mode` set, `spendCapUsd` 2, `categoryAccuracy` from the replay eval, `costPerInvestigationUsd` null until an investigation succeeds |
| Production line, All sites | Every tool Nominal after a reset |
| Run SC-1 | E-02 alarms; Alerts shows one row per active alert and its count matches the Production line; a ticket opens; the investigation ranks ECO-4471 |

## Reset

- Harness > **Reset demo**, or `POST /api/admin/reset` with `{"kind":"hard"}` (admin only). Hard wipes transactional data and replays seed history. `soft` closes open work and keeps history.
- Poll `GET /api/admin/reset/progress` until `running` is false before driving the UI.

## Spend and model

- The model gateway stops live calls at $2 per day (UTC). Over the cap, the UI shows replay mode; plant data is not affected.
- To force saved answers, switch the AI engine to **Replay** in the harness.
- Model access uses managed identity. There are no keys to rotate in the app.

## Alerts and incidents

| Symptom | Action |
|---|---|
| Nothing moves after Run scenario | Wait 30 s for the stream ramp; check the AI engine; reset and run again |
| Alerts count and Production line disagree | Should not happen (one row per active alert, covered by `AlertsPage.test.tsx`). Capture `GET /api/alerts`, reset, and file an issue |
| Investigation stuck or failed | Open it, read the trace; switch to Replay and re-run; tool failures are recorded, never invented |
| Live model slow or 429 | Switch to Replay |
| App down | Check the latest Container Apps revision health; roll back by activating the previous revision |

## Rollback

- App: activate the previous Container Apps revision (blue/green). No data migration is needed; a revision change resets demo data.
- Prompt or model regression: prompts and model versions are versioned; roll back the prompt version without an app rollback.
- Full rebuild: re-run Bicep and `deploy.yml`.

## Disaster recovery

| Item | Demo | Production target |
|---|---|---|
| Data | SQLite on the app volume; a deploy or revision reset rebuilds from seed 4242 | Azure SQL point-in-time restore (7-35 days) and geo-replica |
| Region | Single region (eastus2) | Paired-region failover; at most 15 minutes of data lost, back in service within 1 hour |
| Rebuild | Bicep plus `deploy.yml`, about 15 minutes | Same IaC, plus a tested failover drill per quarter |
| Spend cap | Resets daily (UTC); over cap falls back to replay | Same; plus an Azure budget alert |

## Teardown

Delete the resource group `rg-mps-demo` after the review. Nothing else holds data.

## Eval

`dotnet run --project eval/Eval.Runner` runs the 24 replay cases and writes `eval/results/latest.json`. CI fails when a gate fails or the committed report is stale. See `eval/README.md`.
