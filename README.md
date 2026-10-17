# MPS Quality Agents

**Status: built and hosted.** The console, the agents, the rules engine, the evaluation suite and the Azure deploy are all on `main`. The hosted demo runs one container in resource group `rg-mps-demo`. Its URL is shared by invite.

This repo is a **StatusNeo proposal demo** for a fictional customer, **Meridian Precision Systems (MPS)**, a semiconductor process-equipment maker (thin-film deposition, plasma etch, photoresist strip, wafer clean). The demo is an agentic **Manufacturing Quality & Engineering Issue Resolution** workflow. A simulated tool fleet at three plants streams sensor telemetry, and SPC (Western Electric) rules raise alerts that open tickets. An Investigation Agent and a Resolution Agent work out the root cause from grounded evidence in mock MES/QMS/PLM/ERP/maintenance systems and documents. A CAPA is created **only after explicit human approval**, with full traceability, evaluation and cost tracking.

Design decisions are in [docs/adr](docs/adr/README.md). Bench printouts are ingested as ordinary samples tagged `source=manual` and go through the same rules engine as sensor telemetry (ADR-013). Tool status everywhere is the worst active alert.

> **Built openly with AI.** The code was written by AI coding agents (Grok Build) working in parallel lanes, under human architecture, code review, CI, test and evaluation gates. Every change has an issue on the GitHub Project board and a pull request.

All companies, equipment, people and data in this repo are **fictional and synthetic**.

## What is in it

| Area | Where |
|---|---|
| Console (React) | `web/`. Screens are listed in [docs/demo/SCREENS.md](docs/demo/SCREENS.md) |
| API, live plant, demo harness | `src/Mps.Api` |
| Rules engine (SPC, Western Electric, alert clearing) | `src/Mps.Application/Spc` |
| Lifecycle, approval token, CAPA | `src/Mps.Domain`, `src/Mps.Application` |
| Agents, prompts, evidence gate, guards | `src/Mps.Agents`, `prompts/` |
| Workflow (Microsoft Agent Framework) | `src/Mps.Orchestration` |
| Model gateway (live, replay, spend cap) | `src/Mps.ModelGateway` |
| Simulator, scenarios, seed | `src/Mps.Simulator`, `data/` |
| Mock enterprise systems | `src/Mps.MockEnterprise`, `src/Mps.Connectors` |
| Evaluation | `eval/` ([eval/README.md](eval/README.md)) |
| Tests | `tests/`, `web/src/**/*.test.ts(x)`, `web/e2e`. See [docs/TESTING.md](docs/TESTING.md) |
| Azure | `infra/` (Bicep), `deploy/app.Dockerfile`, `.github/workflows/deploy.yml` |

## Stack

.NET 10 / C# with `TreatWarningsAsErrors`, Microsoft Agent Framework (`Microsoft.Agents.AI.Workflows`) behind `IInvestigationWorkflow` with a LangGraph seam ([docs/langgraph-compat.md](docs/langgraph-compat.md)), `Microsoft.Extensions.AI`, SQLite, SignalR, OpenTelemetry. React 19, TypeScript, Vite, Tailwind and Recharts. Azure Container Apps with Easy Auth (Microsoft Entra), Azure OpenAI `gpt-5.4-mini` through managed identity, Azure Container Registry, Application Insights and a cost budget.

## Clone and run

Replay mode is the default. It needs no model key and makes no network model call.

**One container (Docker).** This is the same image the hosted demo runs.

```powershell
git clone https://github.com/ccc-bcweis/mps-quality-agents
cd mps-quality-agents
Copy-Item .env.example .env
docker compose up --build
```

- Console and API: http://localhost:8080 (signed in as the local dev user)
- API health: http://localhost:8080/health
- Aspire dashboard (traces): http://localhost:18888

**Without Docker** (.NET 10 SDK and Node 22), in two terminals from the repo root:

```powershell
dotnet run --project src/Mps.Api
```

```powershell
cd web
npm ci
$env:VITE_USE_MSW = "false"; npm run dev
```

Open http://localhost:5173. Vite forwards `/api`, `/health` and `/hubs` to the API on port 8080. Plain `npm run dev` without `VITE_USE_MSW=false` runs the console against the mock service worker only, with no API.

In VS Code, F5 runs the **API** configuration and opens `/health`. The dev container is `.devcontainer/devcontainer.json` (.NET 10 and Node 22).

**Live model locally.** Set `LLM_MODE=live` and the Azure OpenAI or OpenAI values in the untracked `.env`. A model key belongs only in that file.

## Tests and evaluation

```powershell
dotnet test MPS.sln
dotnet run --project eval/Eval.Runner
cd web; npm test; npm run lint; npm run build
```

[docs/TESTING.md](docs/TESTING.md) lists the frameworks, the test layers, the guard tests and what CI runs.

## Documents

- [docs/SPEC.md](docs/SPEC.md): design specification (POV, demo, architecture, code, tests, eval)
- [docs/PLAN.md](docs/PLAN.md): execution plan (modules, lanes, phases and gates, test strategy, eval and security, synthetic data, deck)
- [docs/adr](docs/adr/README.md): architecture decisions
- [docs/diagrams](docs/diagrams/README.md): logical, physical, CI/CD, flows, fab floors
- [docs/mocked.md](docs/mocked.md): what is mocked and what is real
- [docs/langgraph-compat.md](docs/langgraph-compat.md): the orchestration seam
- [docs/demo/DEMO-SCRIPT.md](docs/demo/DEMO-SCRIPT.md): presenter demo script
- [docs/demo/SCREENS.md](docs/demo/SCREENS.md): harness screens versus console screens
- [docs/demo/REPRODUCIBILITY.md](docs/demo/REPRODUCIBILITY.md): seed, resets, clock, replay
- [docs/operations/runbook.md](docs/operations/runbook.md): deploy, smoke, reset, rollback, teardown
- [docs/TESTING.md](docs/TESTING.md): test harness
- [eval/README.md](eval/README.md): replay eval (28 cases, CI gate)

## Deploy to Azure

The workflow is manual. It does not run on a pull request or on a push. Bob created resource group `rg-mps-demo` in `eastus2`, the GitHub OIDC app and federated credential, and the GitHub environment `azure-demo` (required reviewer). This repo holds no cloud secret.

The hosted demo is **one container**. `deploy/app.Dockerfile` builds the React console in live API mode (`VITE_USE_MSW=false`, same-origin API) and publishes `Mps.Api`. The image copies `web/dist` into `wwwroot` and `data/` into `/app/content/data`. The API serves those files and falls back to `index.html` for console routes. It holds compiled assemblies and built web files, not the C# or TypeScript source.

Container Apps Easy Auth (Microsoft Entra) runs with anonymous requests allowed, and the app gates access itself. A signed-out browser is sent to `/landing.html` with a **Log in** button. An anonymous API call gets **401**. `GET /health` answers **200**. An account that has not redeemed a join link sees "Signed in as X, which has no access" and **Use a different account** (ADR-011).

SQLite stays inside the container (`/app/data/mps.db`), is seeded on start, and disappears when the replica stops, so **every deploy wipes the demo data**. `deploySql` and `deployStaticWeb` default to **false**. Azure OpenAI is keyless: local auth is off, and the app's managed identity has Cognitive Services OpenAI User. The app settings are the endpoint, the `gpt-5.4-mini` deployment, `Mps__Model__Mode=Live`, and `Mps__Model__DailyCapUsd=2`. The registry is Basic and its admin user is off. The same identity has AcrPull.

**Approval signing key.** Approval tokens are signed with one key, `Mps:Approval:SigningKey` (hex, at least 32 bytes). There is no built-in key. The hosted app reads it from Key Vault secret `approval-signing-key` in `mpsdemokv` through a Container Apps Key Vault reference, using the app identity (Key Vault Secrets User). The secret was created by hand and is not in Git or in the template; a new environment needs it created before the app deploy (`az keyvault secret set --vault-name <vault> --name approval-signing-key --file <(openssl rand -hex 32 | tr -d '\n') -o none`). Outside Development the app will not start without the key. In Development it makes a random key per process, so approvals do not survive a restart.

GitHub environment **secrets** (OIDC login):

- `AZURE_CLIENT_ID`
- `AZURE_TENANT_ID`
- `AZURE_SUBSCRIPTION_ID`

GitHub environment **variables**:

| Name | Purpose |
|---|---|
| `RESOURCE_GROUP` | Target group. Expected value `rg-mps-demo`. |
| `ENTRA_CLIENT_ID` | Easy Auth application (client) id. |
| `ENTRA_ISSUER` | OpenID issuer. |
| `BUDGET_EMAIL` | Budget notification address. Empty skips the budget resource. |

Run it from GitHub: Actions → **Deploy** → Run workflow, then approve the `azure-demo` environment. Leave the image tag blank to use the commit SHA, so the running image tag `mps-app:<sha>` names the commit that is live. The job logs in with OIDC, deploys `infra/main.bicep`, builds `deploy/app.Dockerfile` in the registry, deploys the new image, then checks that `/health` is 200, an anonymous API call is 401 and `/` redirects to `/landing.html`.

Spend notes and the name-prefix rules are in [infra/BUDGET.md](infra/BUDGET.md). Teardown is deleting the resource group. The app runs one replica on purpose, because each replica has its own SQLite file.

## Ownership
All intellectual property in this build (design, specification, plan, code, data, documents and presentation materials) is owned by **Bryan C. Weis / Common Consulting Company**. It is a fictional demonstration built with AI assistance. All companies, equipment and data in it are fictional and synthetic. It is released under the MIT License (see LICENSE). The license grants usage rights but doesn't transfer ownership.

## License
MIT. See [LICENSE](LICENSE).

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
