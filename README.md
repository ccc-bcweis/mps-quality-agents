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
| Rules engine (SPC, Western Electric, alert clearing) | `src/Mps.Application/Spc`. Limits come from the recipe step running at each reading (every step in `data/fleet/*.yaml` defines every parameter, own window or `idle: true`), clamped to the sensor's physical range |
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

.NET 10 (`net10.0`, SDK 10.0.100 in `global.json`, `rollForward` latestFeature) with `TreatWarningsAsErrors`. Microsoft Agent Framework (`Microsoft.Agents.AI` 1.25.0 and `Microsoft.Agents.AI.Workflows`) behind `IInvestigationWorkflow`, with a LangGraph seam ([docs/langgraph-compat.md](docs/langgraph-compat.md)). `Microsoft.Extensions.AI` 10.11.0. EF Core SQLite 10.0.13. ASP.NET Core SignalR on the server and `@microsoft/signalr` 8.0.29 in the console. OpenTelemetry.

The console is React 19.3.1, TypeScript 5.9.3, Vite 7.3.7, Tailwind, and Recharts 2.15.4. Tests use Vitest 3.2.7. Node is 22 in CI (`actions/setup-node`) and in `deploy/app.Dockerfile`. The repo has no `engines` field and no `.nvmrc`.

Hosting is Azure Container Apps in East US 2: one replica, 0.5 vCPU and 1 GiB, image `mps-app` on `aspnet:10.0`. Easy Auth (Microsoft Entra). The hosted model is Azure OpenAI `gpt-5.4-mini` (`MODEL_MODE=azure`). Azure Container Registry, Application Insights, and the budget `mpsdemo-monthly` at $40 a month, with alerts at 80% of actual spend and 100% of the forecast. October spend is $4.13 month to date as of 2026-10-18 about 05:00 Eastern Time (Azure Cost Management, resource group rg-mps-demo). The larger replica is about $33 more per month, approved after the 0.5 GiB replica restarted on failed probes on 18 Oct 2026 (working set 590-673 MiB). Offline mode costs $0.

## Clone and run

No Azure account, model key, Entra tenant or Key Vault is needed. With no model configured the app runs **offline on saved answers**: the real agents, evidence gate and guards run, and the model's part comes from the same saved answers the 33-case replay eval scores, so SC-1 to SC-8 reach their real causes with no network call. The resolution prompt is `prompts/resolution.md` version 1.14.0.

You need either Docker, or the **.NET 10 SDK** (10.0.100) and **Node 22** (the version CI and `deploy/app.Dockerfile` use).

**One container (Docker).** This is the same image the hosted demo runs.

```bash
git clone https://github.com/ccc-bcweis/mps-quality-agents
cd mps-quality-agents
cp .env.example .env          # optional: only needed to set a model key or other values
docker compose up --build
```

```powershell
git clone https://github.com/ccc-bcweis/mps-quality-agents
cd mps-quality-agents
Copy-Item .env.example .env   # optional
docker compose up --build
```

- Console and API: http://localhost:8080 (local sign-in mode: no login, you are the admin)
- API health: http://localhost:8080/health
- Aspire dashboard (traces): http://localhost:18888

Compose publishes these ports on `127.0.0.1` only, because local sign-in mode treats every caller as admin. Compose reads `.env` both for its own `${...}` values (such as `MODEL_MODE`) and as the container's environment.

**Without Docker**, in two terminals from the repo root. `dotnet run` loads the repo's `.env` if there is one (real environment variables win; `MPS_SKIP_DOTENV=1` turns this off).

```bash
dotnet run --project src/Mps.Api
```

```bash
cd web
npm ci
VITE_USE_MSW=false npm run dev
```

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

**Speed.** The plant clock is the wall clock (`Mps:Simulation:RealTime`). The harness offers 1x, 2x, 5x, and 10x, and a sample stride of 5, 15, 30, or 60 seconds. A hard reset returns 1x and 15 seconds (`Mps:Simulation:SampleStrideSeconds`). The kept gap is that stride divided by the speed, and at least one second. The investigation waits for 20 readings after the first rule break (`shapeWait.minReadingsSinceOnset` in `data/metadata/investigation.yaml`) before it judges the shape. At 1x and a 15-second stride that wait is about 5 minutes. An injected fault develops 6 times faster per 1x (`realTimeFaultPace` in `data/metadata/simulation.yaml`). The checked hosted smoke used 10x and a 5-second stride (deploy #14). The SC-1 fault was injected at 03:23:03 Eastern Time. The investigation appeared 5 seconds after that injection, and SC-1 reached `recommendationReady` for an engineering change (ECO-4471) 47 seconds after that injection. SC-8 reached `recommendationReady` for process drift (work order WO-RCP-E03-RF, confidence 0.96, no escalation) 31 seconds after its injection, and its investigation appeared 5 seconds after that injection. SC-1 was `recommendationReady` again on deploy #16 at 04:24 Eastern Time.

**Harness status.** The status line, the scenario picker, and the speed controls follow the server (`runningScenarioId` on the clock and on `GET /api/demo/speed`), in every tab. When nothing is running the line reads **Live**, then the model mode chip, for example `Live · Azure OpenAI`. While a scenario runs it includes `Running SC-n`. There is no Replay/Live toggle for the answer engine.

**Plant data** is a separate control. **Live** follows the running plant. **Replay** shows a recorded scenario (`data/replay/<scenario>/messages.jsonl`) and a banner that the numbers are a recording. Switching it does not change how the model answers.

**Telemetry window.** Each sensor keeps 600 readings, in memory and in the database (`telemetryWindow.readingsPerSensor` in `data/metadata/simulation.yaml`, exposed as `windowPoints`). Older readings roll off. A reset does not seed a week of history. The prime fills the window in memory and does not publish those points on the bus. A hard reset takes seconds.

**Charts.** Limits come from the recipe step running at each reading. Every recipe step defines every parameter (its own window, or `idle: true`). A test fails the build if a step omits one. Limits are clamped to the sensor's physical range, on the server, on the chart, and in the rules score. During a fault the band keeps following the recipe step. It does not re-center on the bad data. The x axis is always 600 readings wide. New readings enter on the right and old ones scroll off the left. Reading numbers keep advancing. Step labels shorten instead of disappearing. Hover names the step and says "Limits for this recipe step". A recipe change draws a divider with the recipe name. The chart header shows the recipe and the step of the newest reading.

**Approval signing key.** In Development with no `Mps__Approval__SigningKey`, the app makes a random key for each run and logs a warning; approvals still work but do not survive a restart. Set your own hex key (at least 32 bytes, for example `openssl rand -hex 32`) in `.env` to keep them. Outside Development the app will not start without a key.

### Model modes

One setting picks the model: `MODEL_MODE` (or `Mps__Model__Mode`, `Mps:Model:Mode` in configuration). Copy `.env.example` to `.env` and set it there. `LLM_MODE` is only a legacy alias (`replay`, `mock`, and `saved` mean offline; `live` means azure). The console shows a read-only **model mode chip** (AI engine). It names the mode, and it says when the gateway is falling back to saved answers and why. There is no Replay/Live toggle for this. `GET /api/model/status` returns the same facts.

| `MODEL_MODE` | Chip | What it needs |
|---|---|---|
| `offline` (default when no key is set) | Offline (saved answers) | Nothing. No network call. |
| `openai` (default when `OPENAI_API_KEY` is set) | OpenAI | `OPENAI_API_KEY`. Optional `OPENAI_MODEL` (default `Mps:Model:OpenAI:Model` in `appsettings.json`, `gpt-5.4-mini`) and `OPENAI_BASE_URL`. |
| `azure` | Azure OpenAI | `AZURE_OPENAI_ENDPOINT`, `AZURE_OPENAI_DEPLOYMENT`. Signs in with managed identity or your `az login` (`DefaultAzureCredential`); `AZURE_OPENAI_API_KEY` is optional. `AZURE_CLIENT_ID` picks a user-assigned identity. |

Put keys only in the untracked `.env` (see `.env.example`), never in a tracked file. A missing value stops startup with a message naming the setting.

```bash
# .env
MODEL_MODE=openai
OPENAI_API_KEY=<your key>
```

A live model call that fails, or takes longer than `Mps:Model:TimeoutSeconds` (20 seconds), is not retried by the SDK. The gateway then fails over to saved answers. After `Mps:Model:CircuitBreakerFailures` failures (2) it stays on saved answers and tries the model again after `Mps:Model:CircuitCooldownSeconds` (60 seconds). While it does, the chip says it is falling back and why. Live calls also stop at `Mps:Model:DailyCapUsd` per day ($2 on the hosted app). Offline answers cost nothing.

### Sign-in modes

| `Mps__Auth__Mode` | Who can get in |
|---|---|
| `local` (default) | Everyone, with no sign-in. The caller is the local admin. Meant for Development on localhost: the app logs a loud warning if it listens beyond loopback (`Mps__Auth__LocalNetworkWarning=false` silences it inside a container whose port is published on loopback) or if it runs outside Development. |
| `azure` (also `entra`) | Microsoft Entra sign-in through Container Apps Easy Auth, using **your own** tenant and sign-in app (`ENTRA_CLIENT_ID`, `ENTRA_ISSUER` at deploy). Admins come from `ADMIN_EMAILS` (or `Mps__Auth__AdminEmails`), comma separated. **The app refuses to start if that list is empty.** `Mps__Auth__HomeTenantId` names your tenant for guest checks. Join links and members use `Mps__Access__Store` (`Sqlite` outside Azure, `AzureTable` on the hosted app), `Mps__Access__TableEndpoint`, `Mps__Access__LinksTable`, and `Mps__Access__MembersTable`. |

No email address is built into the code.

## Tests and evaluation

CI run 39110761511 on `main` `0a3375f` (2026-10-18, 04:15–04:20 Eastern Time) passed **1,144** automated tests: **872** .NET tests passed and **1** skipped, plus **272** Vitest tests in 63 files. The skipped test is `LiveModelEvalTests`, which runs only when `MPS_LIVE_EVAL=1` and a model key are set. The replay eval is separate from that total. On that run it was **29** cases, all passed (category accuracy, safe escalation, injection resistance, expected cause in the top 2, and citation precision all 100%). The suite in this tree is **33** cases, including SC7-03, SC1-01, and SC3-01, scored with the resolution prompt at version 1.14.0. The committed replay report is 33 of 33: decoys in support 0, ruled-out coverage 100%, and the other gate scores at 100%. Live-model samples and gates (`samplesPerCase` 5 for SC7-02, SC7-01, and SC8-01) are in `data/metadata/live-eval.yaml`. Wiring `LiveModelEvalTests` to that file comes in a later round.

Wall time was 5 minutes 13 seconds. Jobs run in parallel: build 5:09, slow tests 3:41, web 1:13, Bicep 8 seconds, gitleaks 6 seconds. CodeQL is a separate workflow (about 4:46 on that day).

```powershell
dotnet test MPS.sln --filter "Category!=Slow"
dotnet test tests/Api.Tests/Api.Tests.csproj --filter "Category=Slow"
dotnet run --project eval/Eval.Runner -- eval/cases eval/results/latest.json
cd web; npm test; npm run lint; npm run build
```

The build job's .NET counts, with `Category!=Slow`: Domain 39, ModelGateway 18, Agents 151, Orchestration 25, MockEnterprise 4, Architecture 16, Simulator 94, Connectors 13, Application 263, Bus 6, Contract 41, Api.IntegrationTests 23, Api.Tests 165 passed and 1 skipped. The slow job is 14 whole-plant tests in `Api.Tests`. [docs/TESTING.md](docs/TESTING.md) lists how to run each suite and what the guard tests catch.

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
- [eval/README.md](eval/README.md): replay eval (33 cases, CI gate; live-model settings in `data/metadata/live-eval.yaml`)

## Deploy to Azure

The workflow is manual `workflow_dispatch` only. A merge or a push does not deploy. Run one deploy per batch, and not while someone is testing the live demo: **a deploy wipes the live quality data**. Join links and members in Table storage stay. Bob created resource group `rg-mps-demo` in `eastus2`, the GitHub OIDC app and federated credential, and the GitHub environment `azure-demo` (required reviewer). This repo holds no cloud secret. A fork or a clone without the environment secrets and `RESOURCE_GROUP` skips cleanly. The federated credential trusts only this repo's `azure-demo` environment.

The hosted demo is **one container**. `deploy/app.Dockerfile` builds the React console in live API mode (`VITE_USE_MSW=false`, same-origin API) and publishes `Mps.Api`. The image copies `web/dist` into `wwwroot` and `data/` into `/app/content/data`. The API serves those files and falls back to `index.html` for console routes. It holds compiled assemblies and built web files, not the C# or TypeScript source.

Container Apps Easy Auth (Microsoft Entra) runs with anonymous requests allowed, and the app gates access itself. A signed-out browser is sent to `/landing.html` with a **Log in** button. An anonymous API call gets **401**. `GET /health` answers **200**. An account that has not redeemed a join link sees "Signed in as X, which has no access" and **Use a different account** (ADR-011).

SQLite stays inside the container (`/app/data/mps.db`), is seeded on start, and disappears when the replica stops, so **every deploy wipes the quality data**. Join links and members live in Table storage (shared key access off, managed identity as Storage Table Data Contributor) and stay across a restart or a deploy. `deploySql` and `deployStaticWeb` default to **false**. Azure OpenAI is keyless: local auth is off, and the app's managed identity has Cognitive Services OpenAI User. The app settings are the endpoint, the `gpt-5.4-mini` deployment, `Mps__Model__Mode=azure`, `Mps__Model__DailyCapUsd=2`, `Mps__Auth__AdminEmails`, and `Mps__Access__Store=AzureTable` with the table endpoint and table names. The registry is Basic and its admin user is off. The same identity has AcrPull.

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
| `NAME_PREFIX`, `LOCATION` | Optional. Resource name prefix and region; blank keeps `infra/main.bicepparam`. |
| `ADMIN_EMAILS` | Comma-separated app admins. The **Run workflow** input `adminEmails` overrides it. If both are blank, the workflow keeps the running app's list; if there is none, it stops before changing anything. |
| `CUSTOM_DOMAIN` | Optional host name. Blank means no custom domain. Set it with `CUSTOM_DOMAIN_CERT_ID`, or leave both blank. See [Add your own domain](#add-your-own-domain). |
| `CUSTOM_DOMAIN_CERT_ID` | Optional full resource id of the managed certificate in the Container Apps environment. Blank means no custom domain. |

If the secrets or `RESOURCE_GROUP` are missing (a fork, a fresh clone), the workflow writes a notice and skips every step; it does not fail.

**Who can deploy here.** The workflow logs in with GitHub OIDC only; the Azure app has no client secret or certificate. Its federated credentials trust only tokens for this repository's `azure-demo` environment, which has a required reviewer, and its roles cover only the demo resource group. A fork or a copy has none of these secrets or variables, and its OIDC token names a different repository, so Azure refuses it even if someone copies the ids.

Run it from GitHub: Actions → **Deploy** → Run workflow, then approve the `azure-demo` environment. Leave the image tag blank to use the commit SHA, so the running image tag `mps-app:<sha>` names the commit that is live. The job logs in with OIDC, deploys `infra/main.bicep`, builds `deploy/app.Dockerfile` in the registry, deploys the new image, then checks that `/health` is 200, an anonymous API call is 401 and `/` redirects to `/landing.html`.

Spend notes and the name-prefix rules are in [infra/BUDGET.md](infra/BUDGET.md). Teardown is deleting the resource group. The app runs one replica on purpose, because each replica has its own SQLite file.

## Deploy to your own Azure

Everything above deploys to the original demo's subscription only. To run your own copy you need an Azure subscription where you can create resources and role assignments (Owner, or Contributor plus User Access Administrator, on the resource group), Azure OpenAI quota for `gpt-5.4-mini` in your region, and globally unique names (`NAME_PREFIX`).

**Path 1: the deploy script from your machine.** Needs the Azure CLI, `openssl` and bash (WSL or Git Bash on Windows).

```bash
az login
az account set --subscription <your-subscription-id>
RESOURCE_GROUP=rg-mps-mine NAME_PREFIX=mpsmine LOCATION=eastus2 deploy/deploy.sh
```

`deploy/deploy.sh` creates the resource group if needed, deploys `infra/main.bicep`, creates the approval signing key in your new Key Vault (random, never printed), builds the image in your registry and deploys the app. **You are the admin by default**: `ADMIN_EMAILS` falls back to the list on the running app, then to your `az login` user. Without `ENTRA_CLIENT_ID` and `ENTRA_ISSUER` the app has no Entra sign-in. To turn it on, create your own sign-in app (next path, step 4), rerun with those two values, then run `deploy/entra-setup.sh <signin-app-id> <app-fqdn> [your-upn]` as printed at the end. The script also passes `CUSTOM_DOMAIN` and `CUSTOM_DOMAIN_CERT_ID` on both template runs. Leave both unset until you add a domain (below).

**Path 2: your own GitHub Actions.** In your fork or copy:

1. Create an Entra app registration (or a user-assigned managed identity) for deploys. Give it **Contributor** and **User Access Administrator** on your resource group (the template assigns roles to the app's identity).
2. Add a federated credential to it: issuer `https://token.actions.githubusercontent.com`, subject `repo:<your-org>/<your-repo>:environment:azure-demo`, audience `api://AzureADTokenExchange`. No client secret is needed.
3. In your repo, create the environment `azure-demo` (add yourself as required reviewer), with secrets `AZURE_CLIENT_ID` (step 1), `AZURE_TENANT_ID`, `AZURE_SUBSCRIPTION_ID`, and variables `RESOURCE_GROUP`, `ADMIN_EMAILS` (your email), `NAME_PREFIX` (your own prefix; registry and Key Vault names are global), and optionally `LOCATION`, `BUDGET_EMAIL`, `CUSTOM_DOMAIN`, and `CUSTOM_DOMAIN_CERT_ID`. Leave the last two blank until you add a domain (below).
4. For sign-in, register your own Entra web app in your tenant and set variables `ENTRA_CLIENT_ID` (its client id) and `ENTRA_ISSUER` (`https://login.microsoftonline.com/<your-tenant-id>/v2.0`). After the first deploy, run `deploy/entra-setup.sh` once as that app's owner.
5. The first deploy needs the approval signing key in your Key Vault. Either run Path 1 once, or create it after the bootstrap step: `az keyvault secret set --vault-name <prefix>kv --name approval-signing-key --file <(openssl rand -hex 32 | tr -d '\n') -o none`. Then run the workflow again.
6. Run **Deploy** from the Actions tab and approve the environment.

In GitHub Actions the deployer is a service principal with no email, so the workflow cannot make "the person deploying" the admin; that is what `ADMIN_EMAILS` (or the `adminEmails` input) is for.

### Add your own domain

The app uses its default host name until you bind your own. A clone leaves `customDomainName` and `customDomainCertificateId` empty, so it gets no custom domain. Set both, or neither. A deploy with only one of them stops with an error instead of dropping the binding.

1. Add a CNAME record from your host name to the app's default host name (the `appFqdn` output).
2. Add a TXT record at `asuid.<host>` whose value is the environment verification id. The TXT record proves you own the domain. Read the id with `az containerapp env show --name <environment> --resource-group <group> --query properties.customDomainConfiguration.customDomainVerificationId -o tsv`.
3. Add the host name to the app: `az containerapp hostname add --hostname <host> --resource-group <group> --name <app>`.
4. Bind it and create the free managed certificate: `az containerapp hostname bind --hostname <host> --resource-group <group> --name <app> --environment <environment> --validation-method CNAME`.
5. On the Entra app, add the redirect URI `https://<domain>/.auth/login/aad/callback`. Run `deploy/entra-setup.sh` first if you use it: that script sets the redirect list to the default host name only, so add this URI after it, and again if you run the script later.
6. Copy the certificate's full resource id: `az containerapp env certificate list --name <environment> --resource-group <group> --managed-certificates-only`.
7. On the GitHub environment `azure-demo`, set `CUSTOM_DOMAIN` to the host name and `CUSTOM_DOMAIN_CERT_ID` to that resource id. Later deploys pass both, so the binding stays. Leave both blank when you do not want a custom domain. `deploy/deploy.sh` reads the same two names.

`<environment>` is `<namePrefix>-cae` and `<app>` is `<namePrefix>-api`.

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
