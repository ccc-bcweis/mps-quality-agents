# Executive point of view: slide content

Copyright (c) 2026 Bryan C. Weis and Common Consulting Company. MIT.

Content for the point-of-view part of the deck (K3): the prioritized opportunities, the deep dive, the metrics and baselines, and the 90-day roadmap. The slide order and the screenshots are in `docs/PLAN.md` section 7. The deck itself is K1 and needs Bryan's sign-off. Every company and figure here is fictional. Money and time figures are illustrative and synthetic, from `data/roi/`.

## Thesis

Engineers decide. AI assembles and reasons over the evidence. Rules enforce. The aim is shorter time to a root cause and fewer repeat defects, with a trail that holds up in an audit. The aim is not fewer engineers.

## Opportunities, prioritized

Score = value × feasibility ÷ risk, each from 1 to 5. The scores are a proposal for review, with the reason stated.

| Rank | Opportunity | Value | Feasibility | Risk | Score | Why |
|---|---|---:|---:|---:|---:|---|
| 1 | Quality investigation and CAPA acceleration | 5 | 4 | 2 | 10.0 | The evidence already exists in MES, QMS, PLM, ERP and maintenance. A person approves every action. This is the demo. |
| 2 | Engineering change impact analysis | 4 | 4 | 2 | 8.0 | Where-used lookups in PLM plus change documents. Read-only, with the same evidence and citation pattern. |
| 3 | Field service copilot | 3 | 4 | 2 | 6.0 | Retrieval over manuals and service history. Low risk, but lower value per answer. |
| 4 | Predictive equipment reliability | 4 | 3 | 3 | 4.0 | Sensor anomalies routed to maintenance. Needs a telemetry pipeline before the model pays off. |
| 5 | Supplier quality and supply-chain risk signals | 4 | 2 | 3 | 2.7 | ERP plus supplier NCRs. Depends on data shared by suppliers. |

## Deep dive: one investigation, today and with agents

Today an engineer queries five systems by hand, passes findings by email, and takes 5 to 15 days to reach a root-cause hypothesis. With agents, intake is automatic, the evidence pack is assembled in minutes, causes are ranked with a citation for every claim, and an engineer reviews and approves before any CAPA opens. The swimlanes are `docs/diagrams/as-is-flow.md` and `docs/diagrams/to-be-flow.md`. The narrative is `docs/analysis/current-vs-future-state.md`.

Who decides what, as built:

| Deterministic rules (code, tested) | AI reasoning (model, evaluated) | Human approval (accountable) |
|---|---|---|
| Intake checks: the tool exists, the fields are complete | Correlating records, logs, notes and procedure text | Accepting a root cause |
| SPC limits from the recipe window, Western Electric rules, alert clearing | Ranking candidate causes with cited evidence | Approving a CAPA |
| Evidence gate: too little or conflicting evidence escalates | Proposing containment and preventive actions | Overriding an escalation, with a written reason |
| Grounding check: every claim cites a retrieved record | Summarizing for the engineer | Closing the CAPA |
| Signal-shape and category guards, read from metadata | | |
| Approval token: single use, tied to the investigation | | |

Anything that must be repeatable, auditable or safety-relevant is code. The model only does the synthesis of mixed, unstructured evidence, where a citation check and a person catch its mistakes. The agents have no write tools.

## Metrics and baselines

Business metrics, with the illustrative baseline for one year at three plants (`data/roi/baseline.csv`) and the expected case (`data/roi/projection.csv`):

| Metric | Baseline | Expected case |
|---|---:|---:|
| Time to root-cause hypothesis | 4.0 days | 1.2 days |
| Incident resolution time (MTTR-Q) | 9.5 days | 6.7 days |
| Repeat-defect rate (same failure mode within 90 days) | 18% | 13.5% |
| CAPA cycle time | 62 days | 40.3 days |
| Engineer hours per investigation | 22 | 12.1 |
| Cost of poor quality | 8% of revenue | Reduced by the savings on the ROI slides |

Real baselines come from a 4-week measurement of the current process in the first month. First-time resolution rate and CAPA on-time closure are measured then too.

AI health metrics, shown on the Metrics panel: completion rate, grounded-answer rate, safe-escalation rate, category accuracy, cost per successful investigation, and spend today against the $2 daily cap. The graded set is 29 replay cases on every CI run, including SC8-01. On CI run 39110761511 (main `0a3375f`, 2026-10-18) every case passed: category accuracy 100%, safe escalation 100%, injection resistance 100%, expected cause in the top 2 100%, citation precision 100%.

## Demo as built

These are the hosted demo, not the illustrative business case above.

| Item | As built |
|---|---|
| Scenarios | 8, SC-1 through SC-8 |
| Answer engine | A read-only model mode chip: Offline (saved answers), OpenAI, or Azure OpenAI. There is no Replay/Live toggle for the model. Plant-data Live/Replay is a separate control and does not change the answers |
| Hosted model | Azure OpenAI `gpt-5.4-mini` (`MODEL_MODE=azure`). Offline mode costs $0. A live call times out at 20 seconds, with no SDK retries, then the gateway fails over to saved answers and the chip says so |
| Harness status | Reads **Live** when no scenario is running, then the chip. The scenario picker and the speed follow the server in every tab |
| Charts | A fixed 600-reading window per sensor. New readings enter on the right and old ones scroll off the left. Limits follow the recipe step |
| Checked smoke | Deploy #14, 10x, sample every 5 seconds. The SC-1 fault was injected at 03:23:03 Eastern Time. The investigation appeared 5 seconds after that injection, and `recommendationReady` for an engineering change (ECO-4471) was 47 seconds after that injection. SC-8: the investigation appeared 5 seconds after its injection, and `recommendationReady` for process drift (the recipe-edit work order on E-03, confidence 0.96, no escalation) was 31 seconds after that injection. SC-1 was `recommendationReady` again on deploy #16 at 04:24 Eastern Time |
| Tests | 1,144 automated tests passing (872 .NET, 272 Vitest in 63 files), plus 1 key-gated skip. CI wall time 5 minutes 13 seconds (build 5:09, slow tests 3:41, web 1:13, Bicep 8 seconds, gitleaks 6 seconds). CodeQL is a separate workflow, about 4:46 |
| Stack | .NET 10 (SDK 10.0.100), Microsoft Agent Framework 1.25.0, Microsoft.Extensions.AI 10.11.0, EF Core SQLite 10.0.13, ASP.NET Core SignalR and `@microsoft/signalr` 8.0.29, React 19.3.1, TypeScript 5.9.3, Vite 7.3.7, Recharts 2.15.4, Vitest 3.2.7, Node 22 |
| Hosting | Azure Container Apps, East US 2, one replica, 0.5 vCPU and 1 GiB (about $33 more per month, always on). The 0.5 GiB replica restarted on 18 Oct 2026 when its working set reached 590-673 MiB. Budget `mpsdemo-monthly` is $40 a month, with alerts at 80% of actual spend and 100% of the forecast. October spend is $4.13 month to date as of 2026-10-18 about 05:00 Eastern Time (Azure Cost Management, resource group rg-mps-demo) |

## 90-day roadmap

| Days | What happens |
|---|---|
| 0 to 30 | Measure baseline metrics. Agree data access. Connect two systems read-only. Build a 50-case evaluation set with engineers. Security review. |
| 31 to 60 | Pilot one tool family at one plant in shadow mode: the agent recommends, engineers work as usual, and the two are compared. Grow the evaluation set to 200 cases. |
| 61 to 90 | Assisted mode with approval-gated CAPA creation. Decide on the second plant. |

Production gates: evaluation thresholds met, a red-team pass, a data-protection review, a runbook, a tested rollback, and more than 60% of pilot engineers using it.

Dependencies: MES and QMS interface access, owners for the document corpus, and a quality sign-off authority.

Adoption: engineer champions, an explanation in every evidence pack, and a no-blame feedback loop where every rejection becomes a new test case.
