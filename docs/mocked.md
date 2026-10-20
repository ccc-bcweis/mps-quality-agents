# What is mocked and what is real

Copyright (c) 2026 Bryan C. Weis and Common Consulting Company. MIT.

Every company, plant, tool, supplier, person and record in this demo is fictional. This page says which parts stand in for a real plant system and which parts are the real product logic. It describes the build on `main`, local and hosted.

## Mocked

| Part | What stands in | Where |
|---|---|---|
| Equipment and telemetry | A seeded simulator streams sensor readings for three fictional plants. Seed 4242 always rebuilds the same fleet. Each sensor keeps a rolling window of 600 readings (`telemetryWindow.readingsPerSensor` in `data/metadata/simulation.yaml`). A reset does not seed a week of history. | `src/Mps.Simulator`, `data/fleet`, `data/kpis` |
| Faults and scenarios | Eight scripted scenarios (SC-1 to SC-8) and a free-form fault injector. The ground truth for each fault is kept away from the agents' tools. | `data/scenarios`, `data/faults`, `data/groundtruth` |
| MES, QMS, PLM, ERP, maintenance | Seeded records (lots, engineering changes, calibrations, work orders, supplier notices) served by read-only tools inside the API. A standalone mock enterprise API with injectable 500, timeout and 409 faults exists for tests and local runs. | `data/seed/enterprise.json`, `src/Mps.MockEnterprise`, `src/Mps.Connectors` |
| CAPA write in the quality system | An in-process writer records the CAPA in the demo database. No outside system is called. | `src/Mps.Api/Fakes` |
| Ticketing | Tickets live in the demo database. | `src/Mps.Api/Persistence` |
| Documents | A synthetic corpus of procedures, bulletins and one poisoned document for the injection test. | `data/docs` |
| Search index | A deterministic hash embedding plus BM25 keyword search, built by `tools/Ingest`. No hosted embedding model. | `data/index`, `src/Mps.Connectors/Docs` |
| Message bus | One in-process event bus. Production would use Service Bus or Event Hubs. | `src/Mps.Bus` |
| Storage | SQLite inside the container for quality data. It is rebuilt from the seed on start, so every deploy wipes that data. Join links and members on the hosted app live in Table storage and stay. | `src/Mps.Api/Persistence`, `src/Mps.Api/Access` |
| Identity, local | Sign-in mode `local` (the default): no login, the caller is the local admin. Meant for localhost; the app warns loudly if it is reachable from the network or runs outside Development. Hosted runs use Microsoft Entra (see Real). | `src/Mps.Api/Auth`, `src/Mps.Api/Access` |
| Return on investment and KPI baselines | Illustrative figures with stated assumptions. | `data/roi`, `docs/roi-assumptions.md` |
| Plant-data replay | Recorded bus messages for each scenario, swapped onto the console on demand. This is the plant-data Live/Replay control. It is not the model. The answer engine is the read-only model mode chip. | `data/replay/SC-*` |

## Real

| Part | What it does | Where |
|---|---|---|
| Rules engine | SPC limits from the recipe window in the fleet data, Western Electric rules, debounce and alert clearing. No limit is written in code. | `src/Mps.Application/Spc` |
| Lifecycle | Alert, ticket, investigation, decision, CAPA stages. Illegal transitions are refused. | `src/Mps.Domain` |
| Approval enforcement | Only a named person can approve. The approval token is single use and checked against the investigation. A decision is final. | `src/Mps.Domain/Approval`, `src/Mps.Application/Decisions` |
| Agent workflow | Microsoft Agent Framework workflow with the evidence gate, a human decision step and one retry when a claim fails a guard. | `src/Mps.Orchestration` |
| Agents and guards | Investigation and Resolution agents, the grounding check (every claim cites a retrieved record), the signal-shape guard and the category guard, both driven by metadata. | `src/Mps.Agents`, `prompts/`, `data/metadata` |
| Model calls | One setting, `MODEL_MODE`: `azure` (the hosted app: Azure OpenAI `gpt-5.4-mini` through managed identity, or a key), `openai` (your `OPENAI_API_KEY`), or `offline`. The console shows a read-only model mode chip. There is no Replay/Live toggle for the answer engine. Calls are pinned to temperature 0, time out at 20 seconds with no SDK retries, and then fail over to saved answers. A cost ledger and a $2 daily cap sit on top. Over the cap, or after repeated failures, the gateway serves saved answers and the chip says so. | `src/Mps.ModelGateway`, `src/Mps.Api/Live`, `infra/` |
| Offline mode (saved answers) | The default with no model configured. The real agents, evidence gate and guards run; the model's part is the saved answers keyed by prompt hash, then the rule-based ranker the 29-case eval scores (`RootCauseRanker`). SC-1 to SC-8 reach their real causes with no network call and no cost. The console chip reads **Offline (saved answers)**. | `data/replay/scripted`, `src/Mps.ModelGateway/Replay`, `src/Mps.Agents/Ranking` |
| Printout reading | Text from a PDF bench printout, or the vision model for an image, becomes one manual sample that goes through the same rules. | `src/Mps.Api/Printouts`, `src/Mps.Connectors/Printouts` |
| Traces and audit | OpenTelemetry traces and an audit log of every step. | `src/Mps.Api` |
| Evaluation | 29 replay cases, including SC8-01, with hard gates on every CI run. On CI run 39110761511 all 29 passed (category accuracy, safe escalation, injection resistance, cause in the top 2, and citation precision all 100%). A key-gated live-model eval (`LiveModelEvalTests`) runs only with `MPS_LIVE_EVAL=1`. | `eval/` |
| Sign-in, hosted | Sign-in mode `azure`: Container Apps Easy Auth with your own Entra tenant and sign-in app, and join links for guests. Admins come only from `ADMIN_EMAILS` / `Mps:Auth:AdminEmails`; the app will not start with none. No email is built into the code. | `infra/`, `src/Mps.Api/Access` |
