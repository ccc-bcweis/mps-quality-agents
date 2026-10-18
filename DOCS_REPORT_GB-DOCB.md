# DOCS_REPORT GB-DOCB

Lane DOC-B. Branch `docs/sync-docb`. Base `0a3375f` (main). Docs only. No code, tests, schemas, or glossary terms were edited. `docs/adr/*.md` bodies were not edited. Bob can drop this file after reading it.

Counts below are CI run 39110761511 (2026-10-18, 04:15–04:20 Eastern Time) unless a sentence says otherwise. Times in these docs say Eastern Time. The letters E-D-T are not a glossary term, and another lane owns `data/glossary.yaml`.

The SC-8 work order id is `WO-RCP-E03-RF`. In `docs/` that id sits in a fenced block, because the glossary scan strips fences and the token inside the id is not a glossary term. `README.md` and `eval/README.md` are outside that scan and name the id in the sentence.

## POV STALE

Checked `docs/deck/pov-content.md` as it was at HEAD `0a3375f`. One product statement was stale. There was no sentence about a Replay/Live answer toggle, and no sentence that the console keeps 7 days of history. The 4-week baseline measurement and the 90-day roadmap (50-case set, then 200) are the engagement plan. They were left as written. The ROI table (revenue, cost of poor quality, MTTR-Q, and the rest) is the illustrative year in `data/roi/`. It was left as written.

Stale statement:

> AI health metrics, shown live on the Metrics panel: completion rate, grounded-answer rate, safe-escalation rate, category accuracy on the graded cases (27 replay cases on every build), cost per successful investigation, and spend today against the cap.

Replacement:

> AI health metrics, shown on the Metrics panel: completion rate, grounded-answer rate, safe-escalation rate, category accuracy, cost per successful investigation, and spend today against the $2 daily cap. The graded set is 29 replay cases on every CI run, including SC8-01. On CI run 39110761511 (main `0a3375f`, 2026-10-18) every case passed: category accuracy 100%, safe escalation 100%, injection resistance 100%, expected cause in the top 2 100%, citation precision 100%.

A new section, "Demo as built", follows that paragraph. It records the 8 scenarios, the read-only model mode chip, the 20-second failover, the harness status reading Live when nothing is running, the 600-reading chart, the deploy #14 and #16 smoke timings, 1,144 passing tests, the CI times, the stack, and the Azure host and budget. Those facts were absent from this file. They are not a second replacement of a stale sentence.

## Files changed

### README.md

- Stack line now names the locked versions: .NET 10 SDK 10.0.100, Microsoft Agent Framework 1.25.0, Microsoft.Extensions.AI 10.11.0, EF Core SQLite 10.0.13, ASP.NET Core SignalR and `@microsoft/signalr` 8.0.29, React 19.3.1, TypeScript 5.9.3, Vite 7.3.7, Recharts 2.15.4, Vitest 3.2.7, Node 22. Removed "Node 20.19 or later also builds". CI and the image use Node 22. There is no `engines` field and no `.nvmrc`.
- Host line: one Container Apps replica, 0.25 vCPU and 0.5 GiB, East US 2, `gpt-5.4-mini`, budget `mpsdemo-monthly` $40 a month (alerts at 80% actual and 100% forecast). October spend about $3.36 as of 17 Oct 2026, 21:23 Eastern Time. Offline mode is $0.
- Speed paragraph no longer says a scenario is about 5 minutes at 1x and under a minute at 10x with a 15-second stride. It now states the harness speeds (1, 2, 5, 10), strides (5, 15, 30, 60), the reset defaults (1x, 15 seconds), the 20-reading shape wait, `realTimeFaultPace` 6, and the checked smoke: deploy #14, 10x, 5-second stride, SC-1 ECO-4471 in 47 seconds, SC-8 process drift on `WO-RCP-E03-RF` at confidence 0.96 with no escalation in about 31 seconds; deploy #16 SC-1 also `recommendationReady`.
- New paragraphs: harness status reads Live when nothing is running, then the chip; plant-data Live/Replay is a separate control; 600-reading window (a reset does not seed a week); chart limits follow the recipe step, the axis stays 600 wide, step labels shorten, hover says "Limits for this recipe step".
- Model section: `.env.example`, `LLM_MODE` as a legacy alias only, read-only chip, no Replay/Live answer toggle, 20-second timeout with no SDK retries then failover, circuit breaker 2 failures / 60 seconds, $2 daily cap on the hosted app.
- Tests: 1,144 passing (872 .NET + 272 Vitest in 63 files, plus 1 key-gated skip), eval 29 at 100%, wall time 5 minutes 13 seconds and the parallel job times, and the `Category!=Slow` / `Category=Slow` commands.
- Deploy: `workflow_dispatch` only, one deploy per batch, not while someone is testing live, a deploy wipes live data, fork-safe OIDC skip, credential trusts only this repo's `azure-demo` environment.

Sign-in (`local` / `azure`, `ADMIN_EMAILS` required in Entra mode, no built-in admin email) was already in the file and was left.

The "Deploy to your own Azure" path still shows the Azure CLI login lines that were already there. No new Azure CLI command was added.

### docs/TESTING.md

Replaced the 2026-10-17 counts (709 .NET, Domain 22, Vitest 198 in 56 files, "passes all 27", "case count is not 27", "four" slow tests) with the CI run above.

- Build job `Category!=Slow`: Domain 39, ModelGateway 18, Agents 151, Orchestration 25, MockEnterprise 4, Architecture 16, Simulator 94, Connectors 13, Application 263, Bus 6, Contract 41, Api.IntegrationTests 23, Api.Tests 165 passed and 1 skipped (858 passed, 1 skipped).
- Slow job `Category=Slow`: 14 Api.Tests, listed by name (8 scenario theories plus 2 more offline facts, baseline quarter hour, reset isolation, plant-data replay, scenario bus chain). The workflow comment still says four. The count of facts is 14. `ci.yml` was not edited.
- How to run: `dotnet test` with both filters, Vitest, `npx vitest run src/glossary`, the eval runner command from `ci.yml`, and `MPS_LIVE_EVAL=1` for `LiveModelEvalTests`.
- Guard rows name golden limits (`EffectiveLimitsGoldenTests`), the recipe-window build check (`RecipeStepLimitsTests`), `CauseGuardTests` (mention versus cite), both glossary scans, and SC8-01.
- CI table includes the job times. Playwright stays 6 tests in 4 specs at 1280, 1600, and 1920, local only.

### docs/deck/pov-content.md

See POV STALE. ROI table and 90-day roadmap unchanged.

### docs/deck/roi-slides.md

Illustrative dollar tables unchanged. Added a note that those figures are not the Azure bill, and a hosted-demo table: model mode chip (no answer-engine Replay/Live toggle), $2 daily cap, $40 budget and the October spend, one replica, eval 29, 1,144 tests, CI wall time.

### docs/mocked.md

- Telemetry row: 600-reading window. A reset does not seed a week. Removed "the same fleet and history" as if start rebuilt a long history.
- Plant-data replay is named as the Live/Replay control, not the model.
- Model-calls row: read-only chip, no answer-engine toggle, 20-second timeout, no SDK retries, then failover.
- Offline ranker: 29-case eval, not 28-case.
- Evaluation row: 29 cases including SC8-01, the 100% scores, and `MPS_LIVE_EVAL=1`.

### docs/live-stream.md

Lead now says `windowPoints` is 600, the axis stays that wide, and the clock carries `runningScenarioId`, speed, and stride. Status reads Live when nothing is running. Subscriptions, message types, the 15-second fallback poll, and the 5-second clock were left.

### docs/demo/DEMO-SCRIPT.md

- Control names checked against main `0a3375f` on 2026-10-18.
- Hard reset finishes in seconds and does not load a week. Speed returns to 1x and the sample stride to 15 seconds. Status reads Live until Run scenario. The model chip is read-only.
- Plant-data Live/Replay stays, and is distinguished from the chip.
- Tool page: recipe and step in the header, fixed 600-wide axis, hover "Limits for this recipe step", one linked-record entry per investigation (the saved record replaces the Started placeholder).
- SC-4 timing now matches `data/scenarios/catalog.yaml`: about 2 minutes at 10x, about 20 minutes at 1x. The old "15 seconds Watch / 1 minute / 4 minutes at 1x" was not in the catalog.
- Escalation: "What the AI proposed (not accepted)" under the evidence gate. Mention versus cite. SC-8 work order id in a fenced block.
- Recovery table: removed "switch to Replay" as the answer-engine fix, and "about 30 seconds" for a hard reset. Timeout is 20 seconds, then failover. Breaker is 2 failures and 60 seconds.
- Likely questions: `gpt-5.4-mini`, no answer toggle, smoke timings, $40 budget, about $3.36.

### docs/demo/SCREENS.md

- Harness row: status reads Live when idle, then the chip. Scenario, speed, and stride follow the server. They are not a private browser choice. No answer-engine Replay/Live toggle. Plant-data Live/Replay remains.
- Tool row: fixed 600-wide axis, recipe-step limits, idle windows, physical clamp, no re-center on a fault, one linked record per investigation.
- Investigation row: rejected answer under the evidence gate.
- Closing paragraph: the replay banner is the plant-data recording. The chip is the model mode.

### docs/demo/REPRODUCIBILITY.md

- Clock, speeds, and strides are cited from `appsettings.json` and `PlantSimulation`, not from `simulation.yaml`. `simulation.yaml` holds fault pace, the 600-reading window, and the recipe schedule. Scenario copy and develop times stay with `data/scenarios/catalog.yaml`.
- Smoke timings for deploy #14 and #16, with the SC-8 work order id in a fence.
- Hard reset primes 600 readings in memory, not on the bus, not a week, returns 1x and 15 seconds, and finishes in seconds.

### docs/operations/runbook.md

- Deploy is manual `workflow_dispatch`. One deploy per batch. Never while Bryan is testing the live demo. A deploy wipes live data. Fork-safe OIDC. The credential trusts only this repo's `azure-demo` environment.
- Admin order: workflow input `adminEmails`, else variable `ADMIN_EMAILS`, else the running app's list, else stop. A local `deploy/deploy.sh` run can also fall back to the signed-in deployer. GitHub Actions has no that fallback.
- Removed "switch the AI engine to Replay", "the UI shows replay mode" over the cap, "Reset demo" as the name of the button, and the 24-case eval command.
- Smoke table: Live plus the chip, metrics `mode` is `live` or `replay` (that field, not the chip), 600 readings, SC-1 and SC-8 timings. Work order id in a fence.
- Eval paragraph: 29 cases including SC8-01, and the `MPS_LIVE_EVAL=1` command.

### docs/analysis/current-vs-future-state.md

The insufficient-evidence path still does not call the resolution agent. Added the later path: after the gate passes, a guard can refuse; a mention that dismisses a ruled-out record is allowed; a citation or naming that record as the cause of its own class escalates; the refused text is "What the AI proposed (not accepted)". Illustrative durations and ROI figures were not rewritten.

### docs/roi-assumptions.md

"Deck slides 2 and 6" was wrong. PLAN section 7 and `roi-slides.md` use slides 3 and 9. The $180,000 platform line is called out as not the Azure `$40` a month demo budget. Dollar tables unchanged.

### docs/langgraph-compat.md

After the insufficient-evidence sentence: when the gate passes, resolution runs, then grounding, CauseGuard, and CategoryGuard. A refused answer is stored as `rejectedAnswer` and shown under the gate. The workflow still does not write.

### docs/perf/bus-throughput.md

Opening now says the 2026-10-15 numbers are the scale benchmark, not live storage. Live data is 600 readings. Start and reset do not call `LastWeek`. The measured rows, times, and chart percentiles were not changed.

### docs/PLAN.md

Light touch. A banner under the header lists what is superseded as of 18 Oct 2026 on main `0a3375f`. Inline superseded marks on the model-gateway row, the 90-day seeded history, "24 at launch", the live/replay/mock slide line, the Sites-tab slide note, the Approval screenshot note, the spend-cap-to-replay line, and rows A1, U4, E1, Z2, and the P2 exit gate. Section 10 keeps its original paragraph, including the original Azure CLI sentence, under a superseded heading. No new Azure CLI command was added. The plan's ROI figures were not replaced with CI counts.

### eval/README.md

- Runner arguments and the CI command. `EvalSuiteTests` requires 29 cases.
- Current scores on run 39110761511 are 100% on the five gates. Gates themselves stay the thresholds.
- `LiveModelEvalTests` is the on-demand key-gated check. Promptfoo is still not in CI, and it is no longer "the next step" or "added in E1".
- Process-drift row: 3 generated (DRF-01 through DRF-03) plus SC8-01. The equipment row says 4 generated plus CAL-05 and SC7-01. Work order id is `WO-RCP-E03-RF`.

### tests/PENDING.md

`MetricsExposeModeAndSpend` now says the metrics field `mode` is `live` or `replay`, and that this field is not the console chip. The eval note says the suite is 29 cases, including SC8-01. The file date was left at 2026-10-16.

## Checked and not changed

`docs/adr/README.md`. The index lists ADR-001 through ADR-014, and those fourteen files are the directory. Titles match the files. It is a decision-record index, not a current-behavior page. Bodies were not opened for edits.

## Checks

- `npx vitest run src/glossary` from `web/` after `npm ci`: 3 files, 12 tests, all passed (parse, scan, split). That scan reads `web/src`, not these docs.
- `dotnet test tests/Simulator.Tests --filter FullyQualifiedName~GlossaryTests`: 1 passed. That test scans `docs/` and `data/` for acronyms missing from `data/glossary.yaml`.
- `web/node_modules` was created by `npm ci` and is not part of the commit.
