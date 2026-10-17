# What is mocked and what is real

Copyright (c) 2026 Bryan C. Weis and Common Consulting Company. MIT.

Every company, plant, tool, supplier, person and record in this demo is fictional. This page says which parts stand in for a real plant system and which parts are the real product logic. It describes the build on `main`, local and hosted.

## Mocked

| Part | What stands in | Where |
|---|---|---|
| Equipment and telemetry | A seeded simulator streams sensor readings for three fictional plants. Seed 4242 always rebuilds the same fleet and history. | `src/Mps.Simulator`, `data/fleet`, `data/kpis` |
| Faults and scenarios | Eight scripted scenarios (SC-1 to SC-8) and a free-form fault injector. The ground truth for each fault is kept away from the agents' tools. | `data/scenarios`, `data/faults`, `data/groundtruth` |
| MES, QMS, PLM, ERP, maintenance | Seeded records (lots, engineering changes, calibrations, work orders, supplier notices) served by read-only tools inside the API. A standalone mock enterprise API with injectable 500, timeout and 409 faults exists for tests and local runs. | `data/seed/enterprise.json`, `src/Mps.MockEnterprise`, `src/Mps.Connectors` |
| CAPA write in the quality system | An in-process writer records the CAPA in the demo database. No outside system is called. | `src/Mps.Api/Fakes` |
| Ticketing | Tickets live in the demo database. | `src/Mps.Api/Persistence` |
| Documents | A synthetic corpus of procedures, bulletins and one poisoned document for the injection test. | `data/docs` |
| Search index | A deterministic hash embedding plus BM25 keyword search, built by `tools/Ingest`. No hosted embedding model. | `data/index`, `src/Mps.Connectors/Docs` |
| Message bus | One in-process event bus. Production would use Service Bus or Event Hubs. | `src/Mps.Bus` |
| Storage | SQLite inside the container. It is rebuilt from the seed on start, so every deploy wipes the demo data. | `src/Mps.Api/Persistence` |
| Identity, local | A local dev user. Hosted runs use Microsoft Entra (see Real). | `src/Mps.Api/Auth` |
| Return on investment and KPI baselines | Illustrative figures with stated assumptions. | `data/roi`, `docs/roi-assumptions.md` |
| Plant-data replay | Recorded bus messages for each scenario, swapped onto the console on demand. | `data/replay/SC-*` |

## Real

| Part | What it does | Where |
|---|---|---|
| Rules engine | SPC limits from the recipe window in the fleet data, Western Electric rules, debounce and alert clearing. No limit is written in code. | `src/Mps.Application/Spc` |
| Lifecycle | Alert, ticket, investigation, decision, CAPA stages. Illegal transitions are refused. | `src/Mps.Domain` |
| Approval enforcement | Only a named person can approve. The approval token is single use and checked against the investigation. A decision is final. | `src/Mps.Domain/Approval`, `src/Mps.Application/Decisions` |
| Agent workflow | Microsoft Agent Framework workflow with the evidence gate, a human decision step and one retry when a claim fails a guard. | `src/Mps.Orchestration` |
| Agents and guards | Investigation and Resolution agents, the grounding check (every claim cites a retrieved record), the signal-shape guard and the category guard, both driven by metadata. | `src/Mps.Agents`, `prompts/`, `data/metadata` |
| Model calls, hosted | Azure OpenAI `gpt-5.4-mini` through managed identity, pinned to temperature 0, with a cost ledger and a $2 daily cap. Over the cap the gateway serves saved answers. | `src/Mps.ModelGateway`, `infra/` |
| Replay and mock reasoner | Saved model answers keyed by prompt hash, and a rule-based reasoner when no answer is saved. Local runs use these by default. | `data/replay/scripted`, `src/Mps.ModelGateway/Replay` |
| Printout reading | Text from a PDF bench printout, or the vision model for an image, becomes one manual sample that goes through the same rules. | `src/Mps.Api/Printouts`, `src/Mps.Connectors/Printouts` |
| Traces and audit | OpenTelemetry traces and an audit log of every step. | `src/Mps.Api` |
| Evaluation | 27 replay cases with hard gates on every CI run. | `eval/` |
| Sign-in, hosted | Container Apps Easy Auth with Microsoft Entra, and join links for guests. | `infra/`, `src/Mps.Api/Access` |
