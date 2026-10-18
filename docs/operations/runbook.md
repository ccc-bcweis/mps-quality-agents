# Operations runbook (hosted demo)

Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

Who uses this: the person running the hosted demo in `rg-mps-demo`. Times are the presenter's local time. Every company, plant, and record in the app is fictional.

## Deploy

Deploys are manual. `.github/workflows/deploy.yml` is `workflow_dispatch` only. A merge does not deploy.

1. Merge to `main` with CI green (`ci.yml`: build, format, tests including `Api.IntegrationTests`, the replay eval, web lint/test/build).
2. **One deploy per batch.** Do not deploy while Bryan is testing the live demo. Say so in the batch note before anyone runs the workflow.
3. Run it from Actions → **Deploy** → Run workflow (or `gh workflow run deploy.yml --ref main`). Leave the image tag blank to use the commit SHA.
4. The `azure-demo` environment needs a required reviewer. Approve the pending deployment.
5. The job federates to Azure with OIDC (no stored client secret). A fork or a clone without `AZURE_CLIENT_ID`, `AZURE_TENANT_ID`, `AZURE_SUBSCRIPTION_ID`, and `RESOURCE_GROUP` writes a notice and skips. The federated credential trusts only this repo's `azure-demo` environment.
6. **Every deploy wipes the live demo data.** SQLite lives in the container. The app starts from seed 4242 with one rolling window of 600 healthy readings per sensor (`telemetryWindow.readingsPerSensor` in `data/metadata/simulation.yaml`). It does not seed a week.

Two optional variables let another subscription use its own resource name prefix and region:

```text
NAME_PREFIX
LOCATION
```

**Admins.** The app will not start in Entra mode with an empty admin list. Nothing in the code names a built-in admin. The workflow resolves emails in this order: the Run workflow input `adminEmails`, else the `azure-demo` variable `ADMIN_EMAILS`, else the list already on the running app. If all three are empty it stops before it changes anything. A local `deploy/deploy.sh` run has one more fallback: the signed-in deployer. In GitHub Actions the deployer is a service principal, so that fallback does not exist there. Set `ADMIN_EMAILS` (or pass `adminEmails`) before the first Entra deploy.

## Smoke check after deploy (5 minutes)

| Check | Expected |
|---|---|
| Sign in as an invited guest | Console loads; uninvited accounts get no token |
| Harness status | Reads **Live** when nothing is running, then the model mode chip. On the hosted app the chip says **Azure OpenAI** (`gpt-5.4-mini`) |
| Spend meter | Visible. Under the $2 daily cap after a fresh day |
| `GET /api/metrics` | `mode` is `live` while Azure or OpenAI answers and `replay` for offline saved answers. `spendCapUsd` is 2. `categoryAccuracy` comes from the 29-case replay eval. `costPerInvestigationUsd` stays null until an investigation succeeds |
| Production line, All sites | Every tool Nominal after a reset. Charts hold 600 readings, not a week |
| Run SC-1 at 10x, sample every 5 s | E-02 alarms. The investigation appears 5 seconds after the fault injection and reaches `recommendationReady` for an engineering change (ECO-4471) 47 seconds after that injection. On deploy #14 the fault was injected at 03:23:03 Eastern Time. Deploy #16 at 04:24 Eastern Time also reached `recommendationReady` |
| Run SC-8 the same way | `recommendationReady` for process drift, confidence 0.96, no escalation, 31 seconds after the fault injection on that same deploy #14 run. The investigation appeared 5 seconds after that injection. The work order id is in the block under this table |

The SC-8 work order id on that run:

```text
WO-RCP-E03-RF
```

## Reset

- Harness > **Hard reset**, or `POST /api/admin/reset` with `{"kind":"hard"}` (admin only). Hard wipes transactional data, keeps metadata, and primes 600 healthy readings per sensor in memory. It does not publish that prime on the bus and it does not seed a week. It returns speed to 1x and the sample stride to 15 seconds, and it finishes in seconds. `soft` closes open work and keeps history.
- Poll `GET /api/admin/reset/progress` until `running` is false before driving the UI.

## Spend and model

- The hosted model is Azure OpenAI `gpt-5.4-mini` (`MODEL_MODE=azure`). The console chip is read-only. There is no Replay/Live toggle for the answer engine.
- A live call times out at 20 seconds with no SDK retries, then the gateway fails over to saved answers. After 2 failures it waits 60 seconds before it tries the model again. The chip says it is falling back and why.
- The model gateway stops live calls at $2 per day (UTC). Over the cap it serves saved answers. Plant data is not affected.
- Offline mode (`MODEL_MODE=offline`) is the way to force saved answers. It is a start setting, not a harness button. It costs $0.
- Model access on the hosted app uses managed identity. There are no keys to rotate in the app.
- The Azure budget is `mpsdemo-monthly`, $40 a month, with alerts at 80% of actual spend and 100% of the forecast. October spend is $4.13 month to date as of 2026-10-18 about 05:00 Eastern Time (Azure Cost Management, resource group rg-mps-demo). The app is one Container Apps replica in East US 2, 0.25 vCPU and 0.5 GiB.

## Alerts and incidents

| Symptom | Action |
|---|---|
| Nothing moves after Run scenario | Confirm the status line says `Running` and the scenario id, and that plant data is Live. Wait for the stream. Then reset and run again |
| Alerts count and Production line disagree | Should not happen (one row per active alert, covered by `AlertsPage.test.tsx`). Capture `GET /api/alerts`, reset, and file an issue |
| Investigation stuck or failed | Open it and read the trace. If the evidence gate says escalate, **What the AI proposed (not accepted)** is the rejected answer under that gate. Tool failures are recorded, never invented. Re-run after a reset |
| Live model slow or 429 | Wait for the chip to say it is falling back (20-second timeout, no SDK retry). Plant data stays as it is. Do not look for an answer-engine toggle |
| Linked records shows the same investigation twice | Should not happen. The saved record replaces its Started placeholder, one entry per investigation |
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
| Spend cap | Resets daily (UTC); over the cap the gateway serves saved answers and the chip says so | Same daily cap, plus budget `mpsdemo-monthly` at $40/month (80% actual, 100% forecast) |

## Teardown

Delete the resource group `rg-mps-demo` after the review. Nothing else holds data.

## Eval

`dotnet run --project eval/Eval.Runner` runs the 29 replay cases, including SC8-01, and writes `eval/results/latest.json`. CI fails when a gate fails or the committed report is stale. On CI run 39110761511 all 29 passed. See `eval/README.md`. The key-gated live-model check is `MPS_LIVE_EVAL=1` and `dotnet test tests/Api.Tests --filter FullyQualifiedName~LiveModelEvalTests`. Without the variable that test is skipped.
