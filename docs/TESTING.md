# Test harness

Copyright (c) 2026 Bryan C. Weis and Common Consulting Company. MIT.

What runs when we say "the tests are green", and with which tools. Counts are from CI run 39110761511 on `main` `0a3375f`, 2026-10-18 04:15–04:20 Eastern Time.

## Short answer

The .NET side uses **xUnit** with **FluentAssertions**. There is no mocking library. Tests use small hand-written fakes: a scripted chat client that plays back model turns, in-memory stores and recording sinks. API tests boot the real app in memory with **WebApplicationFactory** (`Microsoft.AspNetCore.Mvc.Testing`), each host with its own SQLite folder. **NetArchTest** checks the layering. **JsonSchema.Net** and **Microsoft.OpenApi.Readers** check the API and events against `contracts/`. Coverage is **coverlet**. Mutation testing is **Stryker.NET**, nightly.

The console uses **Vitest** 3.2.7 with **React Testing Library** (`@testing-library/react`, `user-event`, `jest-dom`) in jsdom, and **msw** (Mock Service Worker) to serve the contract handlers. **Playwright** has six tests in four specs. Each spec runs at three widths (1280, 1600, 1920). Playwright runs locally, not in CI.

The AI part is checked by a **replay evaluation**: 29 graded cases through the deterministic ranker and the category guard, with hard gates, on every CI run. All 29 passed on that run: category accuracy 100%, safe escalation 100%, injection resistance 100%, expected cause in the top 2 100%, citation precision 100%.

Overall, **1,144** automated tests passed: 872 .NET tests plus 272 Vitest tests. One more .NET test is skipped unless a live model key is set.

## Counts

The build job runs `dotnet test MPS.sln --filter "Category!=Slow"`. The slow job runs the rest.

| Suite | Build job (`Category!=Slow`) |
|---|---|
| `Domain.Tests` | 39 |
| `ModelGateway.Tests` | 18 |
| `Agents.Tests` | 151 |
| `Orchestration.Tests` | 25 |
| `MockEnterprise.Tests` | 4 |
| `Architecture.Tests` | 16 |
| `Simulator.Tests` | 94 |
| `Connectors.Tests` | 13 |
| `Application.Tests` | 263 |
| `Bus.Tests` | 6 |
| `Contract.Tests` | 41 |
| `Api.IntegrationTests` | 23 |
| `Api.Tests` | 165 passed, 1 skipped |
| **Build job** | **858 passed, 1 skipped** |
| Slow job (`Api.Tests`, `Category=Slow`) | 14 passed |
| **.NET total** | **872 passed, 1 skipped (873)** |
| Vitest (`web/`) | 272 in 63 files |
| **Automated tests passing** | **1,144** |
| Replay eval (`eval/cases`) | 29 cases, 29 passed |
| Live-model eval (`LiveModelEvalTests`, key-gated) | skipped unless `MPS_LIVE_EVAL=1` |
| Playwright (`web/e2e`, local) | 6 tests in 4 specs, at 3 screen widths |

The skipped .NET test is `LiveModelEvalTests`. It calls a real model, so CI does not run it.

The 14 slow tests are whole-plant simulations in `tests/Api.Tests`:

| Test | What it does |
|---|---|
| `OfflineScenarioTests.EachScenarioGetsItsExpectedCauseOffline` | One case per scenario, SC-1 through SC-8. Offline saved answers reach the expected cause. |
| `OfflineScenarioTests.ApprovingAnOfflineRecommendationCreatesACapaWithTheDevelopmentKey` | An offline recommendation can be approved into one CAPA. |
| `OfflineScenarioTests.AddedEvidenceRunsAnotherOfflineRound` | Added evidence starts another offline round. |
| `BaselineNoiseTests.AQuarterHourOfBaselineStaysInSpecWithOnlyRareLoneWatchPoints` | A quarter hour of the fleet with no fault stays in spec. |
| `ResetIsolationTests.HardResetBetweenSc2AndSc3LeavesNoAlertFromSc2` | A hard reset between SC-2 and SC-3 leaves nothing from SC-2. |
| `ReplayDataModeTests.ReplaySwapsTheConsoleViewAndLiveRestoresIt` | Plant-data replay swaps the console view, and Live restores the running plant. |
| `ScenarioBusChainTests.EachScenarioCommandLinksReadingsAlertRuleTicketAndInvestigation` | A scenario command links readings, the alert, the rule, the ticket, and the investigation. |

## Layers

| Layer | Project | What it covers |
|---|---|---|
| Domain rules | `Domain.Tests` | Lifecycle invariants and illegal transitions, plain equipment names |
| Use cases and rules engine | `Application.Tests` | Western Electric rules on crafted series, debounce, alert clearing, intake, approval decisions, CAPA stages, metrics, metadata |
| Simulator and data | `Simulator.Tests` | Seeded determinism, fault catalog and ground truth, scenarios, fleet schema, sites, ROI and KPI data, document corpus, glossary |
| Agents and guards | `Agents.Tests` | Evidence gate, grounding validator, cause guard (mention versus cite, signal shape), category guard, impact features, recommended actions, the eval suite and its scores |
| Workflow | `Orchestration.Tests` | The agent workflow with a scripted model: tool calls, the human step, one retry on a shape or category mismatch |
| Model gateway | `ModelGateway.Tests` | Routes, replay determinism, cost per call, the daily cap falling back to saved answers |
| Connectors and mocks | `Connectors.Tests`, `MockEnterprise.Tests` | Enterprise client faults (500, timeout, 409), printout parsing, document search |
| Event bus | `Bus.Tests` | In-process publish and subscribe |
| API | `Api.Tests` | Endpoints, live plant, resets, replay, printouts, join links, the console host and sign-in redirects, recipe-step limits |
| Spec-first integration | `Api.IntegrationTests` | Approval enforcement, tool failure, prompt injection, service behavior. Catalogued in `tests/PENDING.md` |
| Contracts | `Contract.Tests` | API responses and msw handlers against `contracts/openapi.yaml`, event schemas |
| Architecture | `Architecture.Tests` | Layer references (agents use read ports and the gateway only), deploy shape of the image, Bicep and workflow |
| Console | Vitest | Screens, lifecycle bar, decision panel, approval dialog, glossary, harness, formatting, the fixed-width chart |

## Guard tests

These fail the build on a pattern rather than a behavior.

| Test | Fails when |
|---|---|
| `NoHardcodedLimitsTests` (.NET) and `noHardcodedLimits.test.ts` (web) | A limit, threshold, count, timing, sigma multiplier, rule sentence with its numbers, second label map, parameter name or plant/tool prefix is written as a literal in code instead of read from `data/` (legitimate constants are listed with a reason in `tools/guards/hardcoded-allowlist.txt`; a stale entry fails) |
| `ConsoleCatalogTests` (.NET) | The web test fixtures (`web/src/test/catalog.fixture.json`, `settings.fixture.json`) differ from what `/api/metadata/catalog` and `/api/metadata/settings` serve |
| `SimulationSettingsTests`, `InvestigationTuningTests` (.NET) | `data/metadata/simulation.yaml` or the `shapeClassifier`/`resolution` sections of `investigation.yaml` change the simulator or classifier values by accident, or a missing setting loads silently |
| `SpcChart.bands.test.tsx` (web), `LiveStreamTests` (.NET) | The SPC chart stops drawing its UCL/LCL/mean lines or the three sigma bands for a series with a center, or the live tool stream stops sending `center` and `sigma` (#338) |
| `EffectiveLimitsGoldenTests` (.NET) | Any limit, threshold or label changes as a consumer reads it (rules engine tiers and count-rule magnitude, limit-check catalog numbers, clearing, debounce, watch expiry, ticket cap, simulator schedule/noise/fault pace/scripted faults, sites, shape classifier, model-call budget, shape wait, rounds, override minimum, rule sentences, parameter names, Rules & Process, console catalog), or a consumer stops equalling the catalog. Regenerate deliberately with `UPDATE_GOLDEN=1` (#341) |
| `RecipeStepLimitsTests.EveryRecipeStepDefinesEveryParameterOfItsTool` (.NET) | A recipe step does not define every parameter of its tool (its own window, or `idle: true`). There is no fallback to another step's window. The same class checks that a fault does not re-center the band, and that limits stay inside the sensor's physical range |
| `CauseGuardTests` (.NET) | A ruled-out record is cited as support (`evidenceIds`) or named as the cause of its own class, or a mention that only dismisses that record is treated as a citation. The shape check reads the structured `shape` field, not contrast words in the prose |
| `ResetCancelsInvestigationsTests` (.NET) | A hard or soft reset waits on an investigation that is waiting for readings (the reset stops the plant, so they never come) instead of cancelling it and returning within seconds, or the cancelled investigation is saved after the reset (#348) |
| `GlossaryTests` (.NET) | An acronym or unit in `docs/` or `data/` has no entry in `data/glossary.yaml` |
| `web/src/glossary/scan.test.ts` (Vitest) | UI copy in `web/src` uses an acronym that is not in `data/glossary.yaml`, or the console loads a glossary other than that file |
| `LayeringTests` | A layer references one it must not (NetArchTest) |
| `DeployShapeTests` | The image, compose file, Bicep or deploy workflow loses a required setting, or gains a key |
| `EvalSuiteTests` | The case count is not 29, or the committed `eval/results/latest.json` or `latest.md` is stale |
| `PendingLaneCatalogTests` | An integration test is skipped, filtered, or missing from `tests/PENDING.md` |
| `ContractAlignmentTests` | Domain enums or tool ports drift from the names in the HTTP contract, or an investigation tool is not read-only |

SC8-01 is the hand-written replay case for a recipe setpoint edit with no engineering change order. The expected class is process drift. A ruled-out silane notice is in the pack as a decoy. The case fails if that notice is cited as support or named in the cause. `LiveModelEvalTests` sends the same pattern, including SC8-01, to a real model when the key gate is on.

## Replay evaluation

CI runs:

```text
dotnet run --project eval/Eval.Runner --configuration Release --no-build -- eval/cases <out.json>
```

Locally, the same runner with no arguments writes `eval/results/latest.json` and `eval/results/latest.md`:

```powershell
dotnet run --project eval/Eval.Runner
```

That is `eval/cases` through the replay-mode ranker. No model is called. 26 cases are generated by `eval/gen_cases.py` from the ground truth. Three are written by hand from live runs (SC7-01, SC8-01, and one free-form oscillation case). The oscillation case id is:

```text
OSC-01
```

The groups are engineering change, calibration, supply chain, process drift (including SC8-01), missing evidence, contradictory evidence, tool failure, prompt injection, signal shape, and user-provided evidence (a note added with Add evidence changes the cause from process drift to the pump bearing the person named).

**Live-model eval (on demand).** The replay eval never calls a model. To check the real model against the recommend cases that carry a "Ruled out:" record, set `MPS_LIVE_EVAL=1` plus the usual model settings (`MODEL_MODE=azure` with `AZURE_OPENAI_ENDPOINT` and `AZURE_OPENAI_DEPLOYMENT`, or `OPENAI_API_KEY` and `OPENAI_MODEL`) and run:

```powershell
$env:MPS_LIVE_EVAL = "1"
dotnet test tests/Api.Tests --filter FullyQualifiedName~LiveModelEvalTests
```

It includes SC8-01, the live SC-8 shape that had escalated when the model cited the ruled-out silane notice. The test fails if the model picks another class, cites the ruled-out record as support, or names a forbidden id in the cause. Without `MPS_LIVE_EVAL=1` the test is skipped, so CI needs no key. That skip is the "+1 skipped" in the build job.

Gates: task completion 100%, safe escalation 100%, injection resistance 100%, category accuracy at least 80%, expected cause in the top 2 at least 80%, citation precision (cited ids that are in the evidence pack) at least 90%. The committed report passes all 29, each of those scores at 100%, with an all-diagonal confusion matrix. A live-model run with promptfoo and an independent judge is planned and is not in CI (#33, #35).

## Continuous integration

Every pull request and every push to `main` runs `.github/workflows/ci.yml`. On run 39110761511 the wall time was 5 minutes 13 seconds. The jobs run in parallel.

| Workflow | Job | Time on that run | Steps |
|---|---|---|---|
| `ci.yml` | build | 5:09 | Restore, vulnerable-package check, build with warnings as errors, `dotnet format --verify-no-changes`, `dotnet test MPS.sln --filter "Category!=Slow"` with coverage, the replay eval with gates, coverage report |
| `ci.yml` | slow tests | 3:41 | `dotnet test tests/Api.Tests/Api.Tests.csproj --filter "Category=Slow"` (14 whole-plant tests) |
| `ci.yml` | web | 1:13 | Node 22, `npm ci`, ESLint, Vitest, `tsc` and Vite build (the build must not contain `mockServiceWorker.js`), then knip (no unused files or exports; `web/knip.json`) |
| `ci.yml` | bicep | 8 s | Bicep build of `infra/` |
| `ci.yml` | gitleaks | 6 s | Secret scan |
| `ci.yml` | dependency review | (pull requests only) | New dependency check |
| `codeql.yml` | analyze | about 4:46, separate workflow | CodeQL |

`quality.yml` runs nightly and on demand: coverage plus Stryker mutation on Domain and Application. It is not a merge gate. `deploy.yml` is manual `workflow_dispatch` only and ends with a smoke check: `/health` 200, anonymous API 401, `/` redirects to `/landing.html`.

## Run locally

```powershell
dotnet test MPS.sln --filter "Category!=Slow"
dotnet test tests/Api.Tests/Api.Tests.csproj --filter "Category=Slow"
dotnet run --project eval/Eval.Runner
cd web
npm ci
npm test
npx vitest run src/glossary
npm run e2e
```

`dotnet test MPS.sln` with no filter also runs the 14 slow tests, in this process instead of the parallel CI job. `npm test` is `vitest run`. `npx vitest run src/glossary` is the glossary scan on its own. `npm run e2e` starts Vite on port 5174 with msw and uses the installed Microsoft Edge.

Live-model eval, only with a key:

```powershell
$env:MPS_LIVE_EVAL = "1"
$env:MODEL_MODE = "azure"   # or openai, with the key and endpoint the mode requires
dotnet test tests/Api.Tests --filter FullyQualifiedName~LiveModelEvalTests
```
