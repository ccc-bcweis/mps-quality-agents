# MPS Quality Agents

**Status: foundation.** The API serves `GET /health`. Agents, the console, and Azure resources are still to be built.

This repo is a **StatusNeo proposal demo** for a fictional customer, **Meridian Precision Systems (MPS)**, a semiconductor process-equipment maker (thin-film deposition, plasma etch, photoresist strip, wafer clean). The demo is an agentic **Manufacturing Quality & Engineering Issue Resolution** workflow. A simulated tool fleet streams sensor telemetry, and SPC (Western Electric) alerts open tickets. An Investigation Agent and a Resolution Agent work out the root cause from grounded evidence in mock MES/QMS/PLM/ERP/maintenance systems and documents. A CAPA is created **only after explicit human approval**, with full traceability, evaluation and cost tracking.

Design decisions are in [docs/adr](docs/adr/README.md). Bench printouts are ingested as ordinary samples tagged `source=manual` and go through the same rules engine as sensor telemetry (ADR-013). Tool status everywhere is the worst active alert.

> **Built openly with AI.** The code will be written by AI coding agents (Grok Build) working in parallel lanes, under human architecture, code review, CI, test and evaluation gates. Progress is tracked on the GitHub Project board and in issues.

All companies, equipment, people and data in this repo are **fictional and synthetic**.

## Plan
- [docs/SPEC.md](docs/SPEC.md): design specification (POV, demo, architecture, code, tests, eval)
- [docs/PLAN.md](docs/PLAN.md): execution plan (modules, lanes/agents, phases and gates, test strategy, eval and security, synthetic data, deck)
- [docs/demo/DEMO-SCRIPT.md](docs/demo/DEMO-SCRIPT.md): presenter demo script
- [docs/operations/runbook.md](docs/operations/runbook.md): deploy, smoke, reset, rollback, teardown
- [eval/README.md](eval/README.md): replay eval (24 cases, CI gate)

## Planned stack
.NET 8 / C#, Microsoft Agent Framework (with a LangGraph-compatible adapter), React + TypeScript, Azure (Container Apps, Azure OpenAI, Entra ID, Key Vault, App Insights), promptfoo + Microsoft.Extensions.AI.Evaluation, Stryker.NET, OWASP ZAP.

## Clone and run

```powershell
git clone https://github.com/ccc-bcweis/mps-quality-agents
cd mps-quality-agents
Copy-Item .env.example .env
docker compose up
```

- API health: http://localhost:8080/health
- Web placeholder: http://localhost:5173
- Aspire dashboard: http://localhost:18888

The web service is a static placeholder until the UI lane lands. Replay mode is the default. A model key belongs only in the untracked `.env`.

Without Docker, from this folder:

```powershell
dotnet run --project src/Mps.Api
dotnet test
```

In VS Code, F5 runs the **API** configuration and opens `/health`. The dev container is `.devcontainer/devcontainer.json` (.NET 8 and Node 22).

## Deploy to Azure

Bryan approved the first hosted deploy. The workflow is manual. It does not run on a pull request or on a push. Bob creates resource group `rg-mps-demo` in `eastus2`, the GitHub OIDC app and federated credential, and the GitHub environment `azure-demo` (required reviewer). This repo holds no cloud secret.

The hosted demo is **one container**. `deploy/app.Dockerfile` builds the React console in live API mode (`VITE_USE_MSW=false`, same-origin API) and publishes `Mps.Api`. The image copies `web/dist` into `wwwroot`. The API serves those files and falls back to `index.html` for console routes. Container Apps Easy Auth (Microsoft Entra) requires a sign-in on that origin. An anonymous `GET /health` returns **401 or 302**.

SQLite stays inside the container (`/app/data/mps.db`), is seeded on start, and disappears when the replica stops. `deploySql` defaults to **false**. `deployStaticWeb` defaults to **false**. Azure OpenAI is keyless: local auth is off, and the app's managed identity has Cognitive Services OpenAI User. The app settings are the endpoint, the `gpt-4o-mini` deployment, `Mps__Model__Mode=Live`, and `Mps__Model__DailyCapUsd=2`. The registry is Basic and its admin user is off. The same identity has AcrPull.

GitHub environment **secrets** (OIDC login):

- `AZURE_CLIENT_ID`
- `AZURE_TENANT_ID`
- `AZURE_SUBSCRIPTION_ID`

GitHub environment **variables**:

| Name | Purpose |
|---|---|
| `RESOURCE_GROUP` | Target group. Expected value `rg-mps-demo`. |
| `ENTRA_CLIENT_ID` | Easy Auth application (client) id. |
| `ENTRA_ISSUER` | OpenID issuer, `https://login.microsoftonline.com/<tenant-id>/v2.0`. |
| `BUDGET_EMAIL` | Budget notification address. Empty skips the budget resource. |

The workflow identity needs permission to deploy that resource group and to run `az acr build` (Contributor on the group covers both). AcrPull on the app identity is what the container uses to pull. It is not the identity that builds.

Run it from GitHub: Actions → **Deploy** → Run workflow. Leave the image tag blank to use the commit SHA. The job logs in with OIDC, deploys `infra/main.bicep` once with the public ASP.NET image so the registry exists, builds `deploy/app.Dockerfile` in the registry as `mps-app:<tag>`, deploys again with `apiImage=<acrLoginServer>/mps-app:<tag>`, then curls `https://<appFqdn>/health` and prints the URL.

Spend notes and the name-prefix rules are in [infra/BUDGET.md](infra/BUDGET.md). Teardown is deleting the resource group. Each replica has its own SQLite file, so more than one replica does not share tickets.

## Ownership
All intellectual property in this build (design, specification, plan, code, data, documents and presentation materials) is owned by **Bryan C. Weis / Common Consulting Company**. It is a fictional demonstration built with AI assistance. All companies, equipment and data in it are fictional and synthetic. It is released under the MIT License (see LICENSE). The license grants usage rights but doesn't transfer ownership.

## License
MIT

## Task ID key

Board and plan task IDs are project-specific shorthand (not an industry standard): a prefix for the work stream plus a sequence number.

| Prefix | Work stream |
|---|---|
| F | Foundation (skeleton, contracts, CI, Docker, VS Code, Bicep) |
| DS | Design (brand, logos, wireframes, prototypes) |
| D | Data and simulation (fleet specs, telemetry, faults, mock enterprise systems, documents, ROI data) |
| A | Agents (model gateway, evidence gate, Investigation/Resolution agents, workflow) |
| P | Platform API (SPC alerts, intake, lifecycle, approval/CAPA, telemetry) |
| U | UI screens |
| T | Independent tests (contract, integration, mutation) |
| E | AI evaluation |
| SEC | Security and red-team testing |
| I | Integration |
| Z | Azure deployment |
| G | Diagrams |
| K | Deck and documentation |
| R | Rehearsal |
| M | Meetings/coordination |
| ...x | Stretch item (e.g. S3x) |
