# Execution Plan: MPS Quality & Engineering Issue Resolution
**Owner:** Bryan Weis (presenting as StatusNeo) · **Architect/reviewer:** Bob · **Builders:** Grok Build agents on bcw2 · **Repo:** `ccc-bcweis/mps-quality-agents` (public) · **Companion:** `docs/SPEC.md`
**Framing:** a StatusNeo proposal to a fictional customer, **Meridian Precision Systems (MPS)**, a semiconductor process-equipment maker (deposition, etch, strip, clean). This repo is built openly by AI coding agents under human architecture, review and CI gates.
**Target:** done in 2-3 working days after the plan is approved. **AI budget:** ~$2/day hosted for ~1 week, plus $5-10 of dev/eval calls.

**Superseded lines (18 Oct 2026, main `0a3375f`).** This file is the execution plan. A line marked *superseded* is not current behavior. Current behavior is the README and the docs it links. In short: `MODEL_MODE` is offline, openai, or azure, shown as a read-only model mode chip, with no Replay/Live answer toggle. Plant-data Live/Replay is a separate control. The harness status reads Live when nothing is running. Telemetry is 600 readings per sensor, not a week and not 90 days. The replay eval is 29 cases. The hosted app is already in `rg-mps-demo`, East US 2, one replica (0.25 vCPU, 0.5 GiB). The budget is `mpsdemo-monthly` at $40 a month. Deploy is manual `workflow_dispatch` only.

## Ownership and IP
All intellectual property in this build (design, specification, plan, code, data, documents and presentation materials) is owned by **Bryan C. Weis / Common Consulting Company**. It is a fictional demonstration built with AI assistance. All companies, equipment and data in it are fictional and synthetic. It is released under the MIT License (see LICENSE). The license grants usage rights but doesn't transfer ownership.

---
## 1. Modules and separation of concerns
| Module | Responsibility | Depends on | May NOT |
|---|---|---|---|
| `Mps.Domain` | Entities, lifecycle state machine, SPC + Western Electric rules, intake normalization, impact features, evidence gate, approval rules | nothing | call I/O, models or frameworks |
| `Mps.Application` | Use cases (RaiseAlert, OpenTicket, StartInvestigation, Approve/Reject, CreateCapa, AdvanceCapa), ApprovalToken | Domain, ports | reference agents or HTTP directly |
| `Mps.Simulator` | Fleet specs, telemetry generator, fault catalog, ground-truth writer | Domain | expose ground truth via any agent-reachable API |
| `Mps.Connectors` | Read adapters (MES/QMS/PLM/ERP/CMMS/ticketing), retrieval, QMS CAPA write adapter | Application ports | be registered as agent tools if they write |
| `Mps.ModelGateway` | `IChatClient` routing (Azure/OpenAI/Replay/MockReasoner) *(superseded: `MODEL_MODE` is offline, openai, or azure; saved answers are offline, not a Replay/Live toggle)*, cost ledger, daily cap | MEAI | contain business rules |
| `Mps.Agents` | Agent definitions, versioned prompts, tool schemas (read-only) | Application ports, Gateway | write state |
| `Mps.Orchestration` | MAF workflow behind `IInvestigationWorkflow`; LangGraph adapter stub | Agents, Domain | bypass gates |
| `Mps.Api` | Minimal API, SignalR, auth, OTel | Application, Orchestration | hold logic |
| `Mps.MockEnterprise` | Mock enterprise HTTP APIs with fault injection | seed data | — |
| `web/` | React app: StatusNeo demo harness around the MPS console (ADR-006) | OpenAPI contract | call mock systems directly |
| `eval/`, `tests/`, `deploy/` | Evaluation, tests, IaC/CI | all | — |
Layering is enforced by a NetArchTest suite in CI (F1), so violations fail the build rather than relying on review.

## 2. Agents, lanes and ownership
| Agent | Role | Lane / scope | Worktree / branch | Load (h) |
|---|---|---|---|---|
| **Bob** | Architect, reviewer, sole merge approver, Azure operator, integrator | ADRs, contracts sign-off, design-question resolution, PR review, I1, Z2, deck | — | ~8 |
| **GB-F** Foundation | Skeleton, contracts, CI, compose, VS Code, Bicep, docs | foundation, infra, docs | `wt-foundation` / `lane/f-foundation` | 9 |
| **GB-D** Design | Brand kit, logo concepts, wireframes, hi-fi prototypes (HTML/Tailwind) | design track | `wt-design` / `lane/d-design` | 3.5 |
| **GB-1** Data & simulation | Fleet, simulator, faults, ground truth, mock APIs, docs corpus, ROI data | data-sim | `wt-data` / `lane/1-data-sim` | 13.5 |
| **GB-2** Agents | Gateway, gates, agents, workflow, replay | agents | `wt-agents` / `lane/2-agents` | 12.5 |
| **GB-3** API | SPC alerting, intake, lifecycle, approval, telemetry | api | `wt-api` / `lane/3-api` | 8 |
| **GB-4** UI | React console | ui | `wt-ui` / `lane/4-ui` | 11.5 |
| **GB-5** QA/Eval/Security | Spec-derived contract + integration tests, mutation, eval, red-team, ZAP | qa, eval, security | `wt-qa` / `lane/5-qa` | 10.5 |
**Concurrency:** at most 5 Grok Build sessions at once (GB-F + GB-D in P0; GB-1..GB-5 in P1). Launch through the standard launcher only; check for running grok processes first; record PIDs in progress.md; no raw git network calls on bcw2; sync via `sync_to_github.ps1`.
**Bob's token discipline:** reviews by `git diff --stat` + targeted diff reading + CI results; briefs are the issue text plus contracts. No re-reading of whole files.

## 3. Phases and gates
| Phase | Content | Exit gate (Bob checks) |
|---|---|---|
| **P0 Foundation + design start** (starts immediately, does not wait on UI design) | F1-F6 in sequence by GB-F; DS1-DS2 in parallel by GB-D | build + CI green; `contracts-v1` tag; Bicep compiles; Bryan picks a logo and approves wireframes |
| **P1 Parallel lanes** | GB-1..GB-4 build; GB-5 writes spec-derived tests *at the same time* | each lane PR: CI green, lane unit tests, T1/T2 tests relevant to lane passing, Bob approval |
| **P2 Integrate + harden** | I1, E1-E2, T3, SEC1, Z1, Z2 (Azure only after Bryan's go) | S1-S4 pass replay + live *(superseded: eight scenarios SC-1 through SC-8; the answer engine is `MODEL_MODE`, not a Replay/Live toggle)*; eval hard gates; ZAP no highs; mutation ≥60% Domain; hosted demo behind Entra |
| **P3 Package** | K1 deck, K2 docs, R1 rehearsal | clean-machine clone-and-run; Bryan sign-off on deck; rehearsal done |
**Contract changes** after `contracts-v1` need a design-question issue + ADR + Bob approval; affected lanes rebase.

## 4. Design track and design-question handling
- **Track (GB-D):** DS1 brand kit → DS2 wireframes (clickable HTML) → DS3 hi-fi prototypes for Fleet + Investigation. GB-4 starts on shell/data plumbing against MSW mocks and applies the theme once DS1 lands, so it doesn't wait on design.
- **Brands:** StatusNeo pitch shell (deck title/closing, "Powered by StatusNeo" footer; text wordmark unless Bryan OKs the logo); **Meridian Precision Systems** customer brand inside the console (dark fab ops look from SPEC §2.0, 3 SVG logo concepts).
- **Design questions:** any agent that hits ambiguity opens an issue labeled `design-question` (template: context, options, recommendation, blocking?), keeps working on non-blocked items, and links it from its PR. Bob resolves within the same day; the decision is recorded as `docs/adr/ADR-NNN-*.md` and the issue is closed with a link. Questions needing Bryan (brand, scope, money) get the label `needs-bryan` and are batched to him by email.

## 5. Test strategy
| Layer | Author | Lives in | Runs |
|---|---|---|---|
| Unit (domain, application, adapters, components) | Each lane agent for its own code | `tests/<Project>.Tests`, `web/src/**/*.test.tsx` | every PR |
| **Contract tests** (OpenAPI, mock enterprise APIs, tool schemas) | **GB-5, from SPEC + contracts, not from code** | `tests/Contract.Tests` | every PR |
| **Integration / behavior** (approval enforcement, bad evidence, tool failures, injection, lifecycle) | **GB-5, from SPEC** (written first; red until lanes satisfy them) | `tests/Api.IntegrationTests` (WebApplicationFactory + Testcontainers-free in-memory) | every PR |
| Architecture (layering, no write tools on agents) | GB-F | `tests/Architecture.Tests` | every PR |
| E2E smoke (S1-S4) | GB-5 | `tests/e2e` (scripted API; Playwright stretch) | main + nightly |
| Mutation (Stryker.NET) | GB-5 config | `stryker-config.json` | nightly + on demand; HTML report artifact |
| LLM eval | GB-5 + GB-2 | `eval/` | PR (replay mode, gated) + manual live |
| Security (CodeQL, gitleaks, dependency review, ZAP baseline, red-team) | GB-F (CI), GB-5 (ZAP, red-team) | `.github/workflows/security.yml`, `eval/redteam` | PR + nightly |
**Why separate authorship:** lane agents grading their own code is self-grading; GB-5 writes tests from the spec's acceptance criteria, so a lane can't pass by testing what it happened to build.
**Quality signals Bryan can inspect:** coverage (Coverlet → ReportGenerator HTML, ≥70% Domain/Application) and **mutation score** (Stryker.NET, ≥60% Domain) published as CI artifacts and summarized in the PR comment.
**Test-review checklist (Bob, in PR template):** tests fail when the behavior is removed (spot-mutate one) · asserts on outcomes, not implementation calls · negative paths covered (missing, contradictory, failure, unauthorized) · no sleeps/time flakiness (inject `TimeProvider`) · deterministic seeds · no network in unit tests · names state the behavior · eval delta reported for prompt/model changes.

## 6. Evaluation, security and data
### 6.1 LLM eval framework choice
| Option | Strengths | Gaps | Verdict |
|---|---|---|---|
| **promptfoo** (MIT) | Declarative YAML test cases, model-graded + deterministic asserts, CI-friendly, built-in **red-team** plugins (prompt injection, jailbreak, PII), provider for any HTTP endpoint | Node tooling in a .NET repo | **Use** for black-box evals of the API + red-team |
| **Microsoft.Extensions.AI.Evaluation** (MIT) | Native .NET, evaluators for groundedness, relevance, coherence, completeness; response caching; HTML reports; fits xUnit | Younger, fewer red-team features | **Use** for in-process eval of agent outputs in `dotnet test` |
| DeepEval (Apache-2.0) | Rich RAG/agent metrics, pytest style | Python runtime | Mention as alternative |
| Ragas (Apache-2.0) | RAG-specific metrics (faithfulness, context precision/recall) | Python; RAG-only | Mention; metric definitions borrowed |
**Recommendation:** promptfoo for scenario/black-box evals and red-teaming against the running API, plus Microsoft.Extensions.AI.Evaluation for .NET-native groundedness/completeness scoring inside the test suite. Both are open source, matching StatusNeo's preference. Custom deterministic scorers (category accuracy, citation precision, safe-escalation) are shared by both via the API's JSON output.
### 6.2 Security testing
CodeQL (C#, JS) · gitleaks · Dependabot + dependency-review action · **OWASP ZAP baseline** (GitHub Action against the compose-started API/web in CI; fails on high) · **promptfoo red-team** (indirect injection via the poisoned document, direct injection via operator text, jailbreak, excessive agency: tries to make the agent create a CAPA) · **PyRIT** campaign (stretch) · approval-bypass integration tests.
### 6.3 Synthetic data generation
1. **Seeded generator** (`tools/DataGen`, a single `--seed`): fleet (8 tools, 2 bays) *(superseded: the running fleet is 32 tools at three plants)*, recipes, lots/wafers, 90 days of history *(superseded: live telemetry is 600 readings per sensor, not 90 days)*, normal noise models per parameter (AR(1) + measurement noise), PM/calibration schedules, suppliers and material lots, prior NCRs/CAPAs.
2. **Fault catalog** (`data/faults/*.yaml`): each entry has a category (engineering change, calibration/alignment, supply-chain material, process drift), signal signature (parameter, shape, magnitude, scope), the **evidence records it writes** (an ECO in PLM, an overdue calibration in CMMS, a new gas lot in ERP + supplier notice, RF-hours wear), optional **confounders** (a plausible but wrong event), and **evidence perturbations** (drop records → missing; conflicting revs → contradictory).
3. **Ground truth** goes to `data/groundtruth/` (not mounted into the API container; access-checked by an architecture test). Eval and scoring only.
4. **Documents:** templated SOPs, FMEA, service bulletins, troubleshooting guides, a supplier CoA, a decoy (conflicting), and an injection doc; generated from Markdown templates with fleet parameters filled in, then human-reviewed (Bob) for realism. Committed to the repo with the embeddings cache.
5. **ROI financial dataset** (§6.5).
6. **Eval derivation:** each eval case = (seed, fault catalog entry, perturbation) → generated incident + hidden expected behavior/category/citations. Growing the eval set = new combinations, not hand-written answers; SME review samples 20% for realism.
### 6.4 Eval set growth
24 at launch *(superseded: 29 replay cases on main, including SC8-01, all passing in CI)* → 60 (combinations × confounders) before pilot → 200+ from shadow-mode traces labeled through approve/reject → continuous regression from every human rejection. Judge-calibration check against human labels (≥85% agreement).
### 6.5 ROI / financial model (simulated, assumptions stated)
Inputs (synthetic, per year, for a 3-plant MPS): revenue $1.2B; COPQ 8% of revenue split into scrap/rework, warranty/field, downtime and investigation labor; 1,400 quality incidents/yr; MTTR-Q baseline 9.5 days; time to root-cause hypothesis 4 days; repeat-defect rate 18%; CAPA cycle time 62 days; engineer loaded cost $95/h, ~22 h per investigation; tool downtime $12k/h for key tools.
Projection levers (conservative / expected): investigation hours −30/−45%; time to hypothesis −50/−70%; repeat defects −15/−25%; CAPA cycle −20/−35%; downtime hours avoided via earlier SPC detection. Output: annual savings range, payback period, cost to run (AI cost per investigation × volume + platform), and sensitivity table. Lives in `data/roi/` as CSV + a generator, rendered on the demo harness return-on-investment panel and on the ROI slides in section 7. All values labeled "illustrative, synthetic".

## 7. Deck plan (PowerPoint, StatusNeo-branded, "Prepared for Meridian Precision Systems")

Slide-by-slide outline. Finish the pictures after a rehearsal, from the running demo. Every money figure is labeled illustrative and synthetic.

**IP notice (footer on every slide, and the whole closing slide):** "© 2026 Bryan C. Weis and Common Consulting Company. All intellectual property owned by Bryan C. Weis / Common Consulting Company. Fictional demonstration, AI-assisted; all companies and data are synthetic."

Shoot the StatusNeo demo harness and the Meridian console as two surfaces (ADR-006). A shot may keep the harness bar in frame so the buyer sees the outer band. Do not crop that bar off and then treat Reset demo, the scenario picker, or the spend line as product features. Do not show the fault injector, return on investment, or metrics as console tabs. The rehearsal list is `docs/demo/SCREENS.md`.

Meridian Precision Systems is a fictional company that builds chip-process tools and runs three plants of its own. The tools are thin-film deposition (MPS-D7), plasma etch (MPS-E7), photoresist strip (MPS-S7), and single-wafer clean (MPS-C7). The eight-tool demo fleet on Northwater is the teaching floor, not a claim about a real site.

| Plant | Name | What it runs | Place name, invented |
|---|---|---|---|
| A | Northwater Fab | Front-end fab. Deposition and etch, plus the original strip and clean tools. | Cedar Reach, Valecrest |
| B | Ironwood Fab | Second front-end fab. The etch line runs hotter. | Ironwood Bench, Eastmere |
| C | Lumen Pilot Works | Photoresist strip, wafer clean, and a pilot line. | Lumen Yard, Westfold |

Scenario SC-5 is one supplier lot that reaches Northwater and Ironwood. It does not reach Lumen.

### Slide 1 — Opener: company overview and site map (K7)

Who Meridian is, in one sentence, then the map. Pins A, B, and C are Northwater Fab, Ironwood Fab, and Lumen Pilot Works.

- Map: `docs/diagrams/mps-sites.svg`. Invented coast. Caption already on the art: fictional coast, not a real city, not to scale. No country name and no real coastline.
- Picture frames, both marked Illustrative. Do not drop in a photo of a real plant.
  - Exterior: `docs/deck/exterior-illustrative.svg`.
  - Cleanroom: `docs/deck/cleanroom-illustrative.svg`.
- Screenshot: none. This slide is the map and the two frames.
- Surface: neither harness nor console.

Leave-behind, not this slide: `docs/diagrams/print/fab-floor-archD.pdf`, the Northwater floor plan. It is a building drawing, not the console tool schematic and not the Azure sheet.

### Slide 2 — The three plants

One plant per column. Role, tool families, and the invented place name. Call out Ironwood etch running hotter than Northwater, and SC-5 as the lot that shows up in both front-end fabs.

- Screenshot: Sites list, then the etch compare. Console. The harness bar may stay in frame.
- Until the Sites tab is on the build, do not fake a console crop. *(superseded: Sites is a console tab.)* Use the site map from slide 1 and say the screen is the Sites API (`GET /api/sites`, one plant, and compare).
- Surface: console.

### Slide 3 — The problem

What a manual investigation costs, in the buyer's words: spoken alarms, retyped tickets, mail threads, a cause taken from the last similar alarm. Show the as-is swimlane. Stage times are simulated.

- Diagram: `docs/diagrams/print/as-is-flow-archD.pdf`.
- Screenshot: return-on-investment panel, baseline side only. Every figure already says illustrative, synthetic.
- Surface: harness.
- Narrative: `docs/analysis/current-vs-future-state.md`.

### Slide 4 — The agent flow

Thesis on the slide title: engineers decide, the model assembles evidence, rules enforce. Same stages as the problem slide, with the agent platform in place of the manual hand-offs. The human still opens the ticket and still approves.

Plain-language operations (ADR-003) are a crop on this slide, not a separate story: a glossary hover, the tool schematic, and one why-it-matters line. A supervisor can read the pack.

- Diagrams: `docs/diagrams/print/to-be-flow-archD.pdf` and, as the screen-order leave-behind, `docs/diagrams/print/investigation-flow-archD.pdf`. Side-by-side benefits: `docs/diagrams/print/as-is-vs-to-be-archD.pdf`. Every duration on those sheets is simulated.
- Screenshot: Investigation, with evidence, the why-it-matters line, and the tool schematic. Console.
- Surface: console.

### Slide 5 — Live demo: scenario run

The presenter picks a named scenario on the harness bar. Reset demo and the live / replay / mock line are visible. *(superseded: the answer engine is a read-only model mode chip. Plant-data Live/Replay remains. The status line reads Live when nothing is running.)* The console then opens on Alerts. The plant is on the ticket.

- Screenshot: harness bar with the scenario picker, then the Alerts screen it opens.
- Surface: harness, then console.

### Slide 6 — Live demo: ranked cause

Four cause classes, one rank, and the signal that would rule the winner out. The resolution agent is reading the scenario's own records, full text included. It does not write.

- Screenshot: Investigation rank and the evidence matrix.
- Surface: console.

### Slide 7 — Live demo: escalation gate

An escalated investigation cannot be approved straight into a corrective and preventive action (ADR-005). Approve stays disabled. The two actions are request more evidence, or override with a written reason.

- Screenshot: Approval, escalation gate panel, note field, both buttons. *(superseded: the Decision section is on the Investigation page. There is no separate Approval tab. A refused model answer is "What the AI proposed (not accepted)" under the evidence gate.)*
- Surface: console.

### Slide 8 — Live demo: corrective and preventive action

After a real approval, or after an override with a reason, the action service writes the record. The agent draft was not the write.

- Screenshot: the corrective and preventive action screen, lifecycle advanced.
- Surface: console.

### Slide 9 — Return on investment (illustrative)

Projected savings, payback, and the sensitivity table. Same panel as slide 3, on the projection side. Do not read a figure as a contract price. Say illustrative, synthetic, out loud.

- Screenshot: return-on-investment panel.
- Surface: harness.
- Source: `docs/roi-assumptions.md` and `data/roi/`.

### Slide 10 — Architecture

Logical sheet and physical sheet. Say the three plants are simulated, the Sites API is how the console reads them, and the harness bar is not part of the product. The model gateway and the daily spend cap sit on the physical sheet.

- Diagrams: `docs/diagrams/print/logical-archD.pdf` and `docs/diagrams/print/physical-archD.pdf`.
- Screenshot, optional and small: the shell, harness bar visible above the console, so the split is obvious. Do not crop the bar off.
- Surface: diagrams. The optional shell crop is harness and console together.

### Slide 11 — Security

Invite-only sign-in. Agents have no write tools. The escalation gate is the control from slide 7. The spend cap trips the demo to replay. *(superseded: over the cap, or after a 20-second timeout, the gateway serves saved answers and the model mode chip says so. There is no Replay/Live answer toggle.)* No secrets in the repo.

- Screenshot: metrics panel, today's spend against the cap.
- Surface: harness.
- The approval token itself was already shown on the console, slides 7 and 8. Do not reshoot it as a harness control.

### Slide 12 — Rollout

Ninety days. Shadow mode, then a gated pilot, then production gates: measured baselines, a security review, and a person still approving every corrective and preventive action. Manual deploy stays a human gate.

- Screenshot: none.
- Diagram, if a picture is needed: `docs/diagrams/print/cicd-archD.pdf`.
- Surface: neither.

### Slide 13 — Closing

The ask, the risks, and the statement that the demo was built with coding agents in parallel lanes. Repeat the IP notice in full. Fictional companies, fictional plants, synthetic data.

- Screenshot: none.
- Surface: neither.

### How the earlier beats map

| Earlier note | Where it sits now |
|---|---|
| K7 company overview | Slide 1 |
| Three plants, once Lane S1 locked the names | Slides 1 and 2 |
| K6 as-is, to-be, and side-by-side | Slides 3 and 4, plus the side-by-side as a leave-behind on slide 4 |
| K5 plain language | Crops on slide 4. Console |
| Live story: scenario, rank, gate, record | Slides 5 through 8 |
| ROI baseline and ROI projection | Slides 3 and 9. Both harness |

## 8. Issue breakdown
Labels: `lane:*`, `phase:*`, `type:test`, `design-question`, `stretch` (plus `type:design|infra|docs|ci`, `needs-bryan`).
| ID | Phase | Lane | Agent | Issue | Depends on | Acceptance | Est h |
|---|---|---|---|---|---|---|---|
| F1 | P0 | foundation | GB-F | Solution skeleton + layered projects + Directory.Build.props + analyzers | - | `dotnet build` clean with TreatWarningsAsErrors; NetArchTest layering test passes | 1 |
| F2 | P0 | foundation | GB-F | Shared contracts: domain entities, Incident, EvidenceItem, tool interfaces, IInvestigationWorkflow, OpenAPI, event schemas | F1 | Bob reviews and tags `contracts-v1`; MSW mocks generated from OpenAPI; ADR-001 written | 1.5 |
| F3 | P0 | foundation | GB-F | CI: build, test, coverage, format, CodeQL, gitleaks, dependency review | F1 | CI green on a PR; required checks set on main | 1 |
| F4 | P0 | foundation | GB-F | docker-compose skeleton + .env.example + Aspire dashboard | F1 | `docker compose up` starts empty services; no secrets in repo | 0.5 |
| F5 | P0 | foundation | GB-F | VS Code: .vscode (extensions, launch, tasks) + optional devcontainer + clone-and-run README stub | F1 | F5 in VS Code launches API + web; devcontainer builds | 0.5 |
| F6 | P0 | infra | GB-F | Infra Bicep (not deployed): RG-scoped modules for Container Apps, SQL serverless, AOAI, Key Vault, App Insights, SWA | F1 | `az bicep build` + what-if lint passes in CI; no deployment | 1.5 |
| DS1 | P0 | design | GB-D | Brand kit: StatusNeo pitch shell + Meridian Precision Systems customer brand, palette, type, 3 logo concepts (SVG) | - | Bryan picks a logo; tokens exported as Tailwind theme | 1 |
| DS2 | P0 | design | GB-D | Wireframes for all screens and flows (fleet, tool detail, alerts, tickets, investigation, approval, CAPA, injector, ROI, metrics) | DS1 | Clickable static HTML prototype reviewed by Bryan | 1.5 |
| DS3 | P1 | design | GB-D | High-fidelity UI prototype of Fleet Overview + Investigation screens | DS2 | Bryan sign-off; becomes U1/U2 reference | 1 |
| D1 | P1 | data-sim | GB-1 | Fleet specs: 4 tool families, 8 tools, operating windows per recipe step | F2 | YAML schema-validated by tests | 1 |
| D2 | P1 | data-sim | GB-1 | Telemetry simulator (seeded, accelerated time) + stream source | D1 | seed determinism test; 8 tools streaming | 2 |
| D3 | P1 | data-sim | GB-1 | Fault catalog + ground-truth writers (4 root-cause categories, confounders, contradiction, missing) | D2 | each fault writes consistent records; ground truth unreachable from agent tools (test) | 2.5 |
| D4 | P1 | data-sim | GB-1 | Seed generator + mock enterprise APIs (MES/QMS/PLM/ERP/CMMS/ticketing) with fault injection | F2 | endpoints match OpenAPI; 500/timeout/409 injectable | 2.5 |
| D5 | P1 | data-sim | GB-1 | Document corpus (12 docs incl. decoy + injection) + ingest + hybrid retrieval | F2 | retrieval tests pass; corpus generated from templates | 2 |
| D6 | P1 | data-sim | GB-1 | ROI financial dataset: baseline COPQ, scrap, downtime, MTTR, CAPA cycle time + projected savings model | F2 | assumptions table in docs; numbers reproducible from seed; feeds deck + ROI screen | 1.5 |
| A1 | P1 | agents | GB-2 | Model gateway: Azure/OpenAI/Replay/MockReasoner *(superseded: `MODEL_MODE` offline, openai, or azure)*, cost ledger, daily spend cap | F2 | cap test; replay determinism; cost per call recorded | 2 |
| A2 | P1 | agents | GB-2 | Impact features + evidence gate + grounding validator | F2 | unit tests per category and per failure case | 2 |
| A3 | P1 | agents | GB-2 | Investigation + Resolution agents, versioned prompts, tool bindings | A1, D4, D5 | scripted tool-call tests; schema-valid output | 2.5 |
| A4 | P1 | agents | GB-2 | MAF workflow + checkpoint + human-in-the-loop resume; LangGraph adapter stub + mapping doc | A2, A3 | S1/S2 pass end to end in replay | 2 |
| P1 | P1 | api | GB-3 | SPC windows + Western Electric rules -> alert -> auto ticket | F2, D2 | rule tests incl. false positives; debounce; ticket created | 2 |
| P2 | P1 | api | GB-3 | Intake channels (IoT, operator, NCR, supplier notice, injector) + dedupe + severity | F2 | one test per channel | 1.5 |
| P3 | P1 | api | GB-3 | Lifecycle state machine + API + SignalR | F2 | illegal transitions rejected | 2 |
| P4 | P1 | api | GB-3 | ApprovalToken + CAPA action + retries/idempotency | P3, D4 | approval-enforcement tests pass | 1.5 |
| P5 | P1 | api | GB-3 | OpenTelemetry + metrics endpoint (completion, grounding, tools, p95, cost) | P3 | metrics visible in Aspire | 1 |
| U1 | P1 | ui | GB-4 | App shell + brand theme + Fleet Overview + Tool Detail with SPC charts | F2, DS3 | matches DS3; Vitest passes | 3 |
| U2 | P1 | ui | GB-4 | Alerts, Tickets, Investigation (evidence, hypotheses, trace) + lifecycle bar | F2, DS2 | works on MSW mocks then live API | 3 |
| U3 | P1 | ui | GB-4 | Approval + CAPA screens (explicit confirm, reject path) | F2, DS2 | approval dialog test | 1.5 |
| U4 | P1 | ui | GB-4 | Fault Injector + mode banners (LIVE/REPLAY) *(superseded: model mode chip; plant-data Live/Replay remains; status reads Live when idle)* + spend meter + metrics + ROI screen | F2, D6 | inject -> alert visible within 10 s | 2 |
| T1 | P1 | qa | GB-5 | Contract tests from OpenAPI + spec (written from SPEC, not code) | F2 | runs in CI against each service; failures block merge | 2 |
| T2 | P1 | qa | GB-5 | Spec-derived integration tests: approval enforcement, bad evidence, tool failures, injection | F2 | tests authored before lane code lands; red until lanes satisfy them | 2.5 |
| T3 | P2 | qa | GB-5 | Mutation testing (Stryker.NET) on Domain + Application + coverage report published | I1 | mutation score >= 60% on Domain; HTML report as CI artifact | 1 |
| I1 | P2 | integration | Bob | Integration: merge lanes; S1-S4 green in replay and live | A4, P4, U4, D3 | scripted smoke passes; Bob sign-off | 2 |
| E1 | P2 | eval | GB-5 | Eval: 24 cases *(superseded: 29 cases in `eval/cases`, run by `eval/Eval.Runner` on every CI run; promptfoo is not the CI runner)* + promptfoo config + MEAI.Evaluation runner + report + CI gate | I1 | report table + confusion matrix; hard gates enforced | 2.5 |
| E2 | P2 | eval | GB-2 | Record replay set from live runs | I1 | local replay reproduces S1-S4 | 1 |
| SEC1 | P2 | security | GB-5 | Security: OWASP ZAP baseline in CI + promptfoo red-team (injection/jailbreak) suite | I1 | ZAP no high findings; red-team pass rate reported | 1.5 |
| Z1 | P2 | infra | GB-F | Deploy workflow: OIDC to Azure, container build/push, Bicep deploy (gated, manual trigger) | F6, I1 | deploy.yml ready; runs only after Bryan approves the Azure step | 1.5 |
| Z2 | P2 | infra | Bob | Azure stand-up: RG, Entra invite-only app, spend cap + budget alert | Z1 | uninvited user blocked; cap trips to replay *(superseded: saved answers, and the chip says so)*; Bryan approved | 1.5 |
| K1 | P3 | deck | Bob | PowerPoint deck: outline now, finish with screenshots after demo | I1 | Bryan sign-off | 0 |
| K2 | P3 | docs | GB-F | README clone-and-run, mocked.md, langgraph-compat.md, ADRs, diagrams, AI-built statement | I1 | clean-machine run from README alone | 1.5 |
| R1 | P3 | rehearsal | Bryan | Rehearsal + backup video | K1, Z2 | 60-min run-through done | 0 |
| S1x | P2 | data-sim | GB-1 | Stretch: process-drift scenario S5 + CAPA verification auto-check *(stretch)* | I1 | - | 2 |
| S2x | P2 | agents | GB-2 | Stretch: MCP server exposing tools + Playwright smoke *(stretch)* | I1 | - | 2 |
| S3x | P2 | agents | GB-2 | Stretch: local SLM route (Ollama) *(stretch)* | A1 | - | 1 |
| S4x | P2 | ui | GB-4 | Stretch: visual polish pass *(stretch)* | U1, U2, U3, U4 | - | 2 |
| S5x | P2 | security | GB-5 | Stretch: PyRIT jailbreak campaign *(stretch)* | SEC1 | - | 1 |

**Totals:** 45 issues; must-have ~64 h of agent time, stretch ~8 h. With 5 lanes in parallel the elapsed build is ~16-20 h; Bob ~8 h; Bryan ~8-10 h (approvals, design picks, deck, rehearsal).

## 9. Calendar (from plan approval)
Day 1: P0 (foundation + design), contracts frozen by midday; P1 lanes start. Day 2: P1 lanes finish; integration; eval; security; Azure stand-up after Bryan's go. Day 3: deck, docs, hardening, rehearsal. Cut order: stretch → supplier channel → AI Search (in-proc index) → CAPA stages beyond Open → S4 presets only.

## 10. Azure plan (not created yet)

*(superseded: the demo is deployed. Resource group `rg-mps-demo`, East US 2, GitHub environment `azure-demo`, manual `workflow_dispatch`, fork-safe OIDC. One Container Apps replica, 0.25 vCPU and 0.5 GiB. Model `gpt-5.4-mini`. Gateway cap $2/day. Budget `mpsdemo-monthly` is $40 a month, with alerts at 80% of actual spend and 100% of the forecast, not a $10/week grain. October spend is $4.13 month to date as of 2026-10-18 about 05:00 Eastern Time (Azure Cost Management, resource group rg-mps-demo). Admins come from `ADMIN_EMAILS`. The paragraph below is the original plan.)*

Resource group `rg-mps-demo` in Bryan's subscription (region with Azure OpenAI quota, e.g. East US 2), its own Entra app registration (invite-only, assignment required), managed identity for the apps, OIDC federated credential for GitHub Actions on `ccc-bcweis/mps-quality-agents` (main + environment `demo`, with required reviewer). Spend controls: gateway hard cap $2/day, AOAI TPM quota, budget alert at $10/week. Teardown: delete the RG after the review. **Open:** Azure OpenAI availability couldn't be checked (no Azure CLI session on the box); Bryan or Bob should run `az cognitiveservices account list` / check model quota before Z2.
