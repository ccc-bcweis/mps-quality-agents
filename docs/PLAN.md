# Execution Plan: MPS Quality & Engineering Issue Resolution
**Owner:** Bryan Weis (presenting as StatusNeo) · **Architect/reviewer:** Bob · **Builders:** Grok Build agents on bcw2 · **Repo:** `ccc-bcweis/mps-quality-agents` (public) · **Companion:** `docs/SPEC.md`
**Framing:** a StatusNeo proposal to a fictional customer, **Meridian Precision Systems (MPS)**, a semiconductor process-equipment maker (deposition, etch, strip, clean). This repo is built openly by AI coding agents under human architecture, review and CI gates.
**Target:** done in 2-3 working days after the plan is approved. **AI budget:** ~$2/day hosted for ~1 week, plus $5-10 of dev/eval calls.

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
| `Mps.ModelGateway` | `IChatClient` routing (Azure/OpenAI/Replay/MockReasoner), cost ledger, daily cap | MEAI | contain business rules |
| `Mps.Agents` | Agent definitions, versioned prompts, tool schemas (read-only) | Application ports, Gateway | write state |
| `Mps.Orchestration` | MAF workflow behind `IInvestigationWorkflow`; LangGraph adapter stub | Agents, Domain | bypass gates |
| `Mps.Api` | Minimal API, SignalR, auth, OTel | Application, Orchestration | hold logic |
| `Mps.MockEnterprise` | Mock enterprise HTTP APIs with fault injection | seed data | — |
| `web/` | React console (MPS-branded, "Powered by StatusNeo") | OpenAPI contract | call mock systems directly |
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
| **P2 Integrate + harden** | I1, E1-E2, T3, SEC1, Z1, Z2 (Azure only after Bryan's go) | S1-S4 pass replay + live; eval hard gates; ZAP no highs; mutation ≥60% Domain; hosted demo behind Entra |
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
1. **Seeded generator** (`tools/DataGen`, a single `--seed`): fleet (8 tools, 2 bays), recipes, lots/wafers, 90 days of history, normal noise models per parameter (AR(1) + measurement noise), PM/calibration schedules, suppliers and material lots, prior NCRs/CAPAs.
2. **Fault catalog** (`data/faults/*.yaml`): each entry has a category (engineering change, calibration/alignment, supply-chain material, process drift), signal signature (parameter, shape, magnitude, scope), the **evidence records it writes** (an ECO in PLM, an overdue calibration in CMMS, a new gas lot in ERP + supplier notice, RF-hours wear), optional **confounders** (a plausible but wrong event), and **evidence perturbations** (drop records → missing; conflicting revs → contradictory).
3. **Ground truth** goes to `data/groundtruth/` (not mounted into the API container; access-checked by an architecture test). Eval and scoring only.
4. **Documents:** templated SOPs, FMEA, service bulletins, troubleshooting guides, a supplier CoA, a decoy (conflicting), and an injection doc; generated from Markdown templates with fleet parameters filled in, then human-reviewed (Bob) for realism. Committed to the repo with the embeddings cache.
5. **ROI financial dataset** (§6.5).
6. **Eval derivation:** each eval case = (seed, fault catalog entry, perturbation) → generated incident + hidden expected behavior/category/citations. Growing the eval set = new combinations, not hand-written answers; SME review samples 20% for realism.
### 6.4 Eval set growth
24 at launch → 60 (combinations × confounders) before pilot → 200+ from shadow-mode traces labeled through approve/reject → continuous regression from every human rejection. Judge-calibration check against human labels (≥85% agreement).
### 6.5 ROI / financial model (simulated, assumptions stated)
Inputs (synthetic, per year, for a 3-plant MPS): revenue $1.2B; COPQ 8% of revenue split into scrap/rework, warranty/field, downtime and investigation labor; 1,400 quality incidents/yr; MTTR-Q baseline 9.5 days; time to root-cause hypothesis 4 days; repeat-defect rate 18%; CAPA cycle time 62 days; engineer loaded cost $95/h, ~22 h per investigation; tool downtime $12k/h for key tools.
Projection levers (conservative / expected): investigation hours −30/−45%; time to hypothesis −50/−70%; repeat defects −15/−25%; CAPA cycle −20/−35%; downtime hours avoided via earlier SPC detection. Output: annual savings range, payback period, cost to run (AI cost per investigation × volume + platform), and sensitivity table. Lives in `data/roi/` as CSV + a generator, rendered on the console's ROI screen and in deck slide 5. All values labeled "illustrative, synthetic".

## 7. Deck plan (PowerPoint, StatusNeo-branded, "Prepared for Meridian Precision Systems")
Draft outline now; finish after the demo with real screenshots and measured metrics.
The opening slide is the company overview (K7), before the numbered story. The numbers below are unchanged.
1. Title + MPS context (fleet, plants, pain) · 2. The problem in numbers (ROI baseline) · 3. Thesis: engineers decide, AI assembles evidence, rules enforce · 4. Opportunity landscape (5, prioritized) · 5. Deep dive: current vs future state + Rules/AI/Human table · 6. Business case: KPIs, baselines, projected savings, payback · 7. Architecture (logical + physical; MAF ↔ LangGraph) · 8. 90-day roadmap + production gates · 9. How we built it: AI agents in parallel lanes, Project board, CI/test/eval evidence, open statement that it's AI-built · 10. Risks, controls and ask (closing slide carries the IP notice).
**IP notice (footer on every slide + closing slide):** "© 2026 Bryan C. Weis and Common Consulting Company. All intellectual property owned by Bryan C. Weis / Common Consulting Company. Fictional demonstration, AI-assisted; all companies and data are synthetic."
(Core exec story = slides 1-6 + 8, per the 6-8 recommendation; 7 and 9 support the architecture and code segments. K7 opens, ahead of slide 1.)

- **Company overview slide (K7):** Opening slide. Meridian Precision Systems is a fictional company that builds chip-process tools and runs three plants of its own. The tools are thin-film deposition (MPS-D7), plasma etch (MPS-E7), photoresist strip (MPS-S7), and single-wafer clean (MPS-C7). The eight-tool demo fleet is the teaching floor, not a claim about a real site.

  Three plants. Names are proposals until Lane S1 locks them. Do not swap in a real city.

  | Plant | Name | What it runs |
  |---|---|---|
  | A | Cindervane | Front-end fab. Deposition and etch. |
  | B | Lowspindle | Second fab. A similar tool mix. |
  | C | Rookspur | Photoresist strip, wafer clean, and a pilot line. |

  Map: `docs/diagrams/mps-sites.svg`. A stylized invented coast with three pins. Caption on the art: "Illustrative. Fictional coast. Not a real city. Not to scale." No country name, no real fab city, no real coastline.

  Picture frames, both marked Illustrative. Do not drop in a photo of a real plant. Prompts below are for a later image pass. Each finished picture stays labeled Illustrative.

  - Exterior frame: `docs/deck/exterior-illustrative.svg`. Prompt: "Wide daylight view of a low modern factory campus beside a generic rocky shore. Flat-roofed industrial buildings, a small office block, a parking row, and young trees. Cool overcast light. No logos, no readable signs, no flag, no city skyline, no famous landmark, no real company. Label the picture Illustrative."
  - Cleanroom frame: `docs/deck/cleanroom-illustrative.svg`. Prompt: "View down a wide chip-factory cleanroom aisle. People in full white cleanroom suits. Stainless process tools with round chambers and square load ports. A thin overhead rail carrying a wafer carrier. Pale light, smooth floor. No logos, no readable screens, no real company marks. Label the picture Illustrative."

  Leave-behind, not this slide: the physical floor plan `docs/diagrams/print/fab-floor-archD.pdf` (plan view of the Cindervane demo floor). It is a building drawing, not the console tool schematic and not the Azure architecture sheet.

- **Design principle slide (K5 #66):** Plain-language operations (ADR-003): a supervisor or manager who isn't an engineer can understand and explain a problem, because nothing is in code. Show glossary hovers, the tool schematic, why-it-matters evidence and plain-word metrics.
- **Current versus future slides (K6 #75):** Three slides. 1. As-is swimlane, with manual hand-offs and error risks (`docs/diagrams/print/as-is-flow-archD.pdf`). 2. To-be swimlane (G6), the same stages and lanes plus the MPS agent platform (`docs/diagrams/print/to-be-flow-archD.pdf`). 3. Side-by-side with simulated benefits (`docs/diagrams/print/as-is-vs-to-be-archD.pdf`). Narrative and the stage table: `docs/analysis/current-vs-future-state.md`. Every figure on these slides is labeled simulated. The G4 screen-order sheet (`docs/diagrams/print/investigation-flow-archD.pdf`) stays the architecture flow. It is not the K6 to-be slide.

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
| A1 | P1 | agents | GB-2 | Model gateway: Azure/OpenAI/Replay/MockReasoner, cost ledger, daily spend cap | F2 | cap test; replay determinism; cost per call recorded | 2 |
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
| U4 | P1 | ui | GB-4 | Fault Injector + mode banners (LIVE/REPLAY) + spend meter + metrics + ROI screen | F2, D6 | inject -> alert visible within 10 s | 2 |
| T1 | P1 | qa | GB-5 | Contract tests from OpenAPI + spec (written from SPEC, not code) | F2 | runs in CI against each service; failures block merge | 2 |
| T2 | P1 | qa | GB-5 | Spec-derived integration tests: approval enforcement, bad evidence, tool failures, injection | F2 | tests authored before lane code lands; red until lanes satisfy them | 2.5 |
| T3 | P2 | qa | GB-5 | Mutation testing (Stryker.NET) on Domain + Application + coverage report published | I1 | mutation score >= 60% on Domain; HTML report as CI artifact | 1 |
| I1 | P2 | integration | Bob | Integration: merge lanes; S1-S4 green in replay and live | A4, P4, U4, D3 | scripted smoke passes; Bob sign-off | 2 |
| E1 | P2 | eval | GB-5 | Eval: 24 cases + promptfoo config + MEAI.Evaluation runner + report + CI gate | I1 | report table + confusion matrix; hard gates enforced | 2.5 |
| E2 | P2 | eval | GB-2 | Record replay set from live runs | I1 | local replay reproduces S1-S4 | 1 |
| SEC1 | P2 | security | GB-5 | Security: OWASP ZAP baseline in CI + promptfoo red-team (injection/jailbreak) suite | I1 | ZAP no high findings; red-team pass rate reported | 1.5 |
| Z1 | P2 | infra | GB-F | Deploy workflow: OIDC to Azure, container build/push, Bicep deploy (gated, manual trigger) | F6, I1 | deploy.yml ready; runs only after Bryan approves the Azure step | 1.5 |
| Z2 | P2 | infra | Bob | Azure stand-up: RG, Entra invite-only app, spend cap + budget alert | Z1 | uninvited user blocked; cap trips to replay; Bryan approved | 1.5 |
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
Resource group `rg-mps-demo` in Bryan's subscription (region with Azure OpenAI quota, e.g. East US 2), its own Entra app registration (invite-only, assignment required), managed identity for the apps, OIDC federated credential for GitHub Actions on `ccc-bcweis/mps-quality-agents` (main + environment `demo`, with required reviewer). Spend controls: gateway hard cap $2/day, AOAI TPM quota, budget alert at $10/week. Teardown: delete the RG after the review. **Open:** Azure OpenAI availability couldn't be checked (no Azure CLI session on the box); Bryan or Bob should run `az cognitiveservices account list` / check model quota before Z2.
