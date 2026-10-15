# Lane report: GB-3 round 3

Branch `lane/r2-reset`. Issue #83. Local commits only. Nothing was pushed.

Copyright (c) 2026 Bryan C. Weis and Common Consulting Company. MIT.

| Work | Commit | What landed |
|---|---|---|
| Scripted scenarios and the demo clock | `11638a1` | Four named scenarios, one per root-cause class. Telemetry is a function of `Mps:Demo:Seed` (default 4242) and the baseline origin 2026-09-01T00:00:00Z. Reset clears injected faults and rewinds the clock. |
| Model pin and scripted replay | `49ea218` | Live calls use temperature 0 and the configured seed. `Mps:Model:ScriptedReplay` serves `data/replay/scripted/{scenarioId}/{promptHash}.json` and falls through to live on a miss. |
| Admin reset and scenario routes | `098153b` | `POST /api/admin/reset` is admin-only. `GET /api/admin/session`, `GET /api/injector/scenarios`, and `POST /api/injector/scenarios/{scenarioId}` are additive. OpenAPI 0.3.0. ADR-001 addendum is contracts v1.2. `POST /api/faults` still accepts a free-form fault. |
| Reset demo button and scenario picker | `ae7f15d` | The header account menu shows Reset demo for an admin, with a confirmation that names fleet state, tickets, investigations, approvals and corrective actions, and the simulated clock. The injector keeps the free-form form and adds a scenario picker. |
| Reproducibility note | `b8150aa` | `docs/demo`. Live model wording may vary slightly. |

Base is `f86cf6f`. This lane did not edit `web/src/components/ToolSchematic*`, `web/src/fleet/schematic.ts`, or `deploy/infra`. No design question blocked the work. The OpenAPI change is the additive admin path the issue asked for.

## Reset

`DemoResetService` deletes approvals, corrective actions, investigations, tickets, incidents, alerts, faults, metric samples, chambers, tools, and the ticket sequence, then reseeds the eight-tool fleet and sets the next ticket number to 1000. The first new ticket is TKT-1001. The simulated clock returns to 2026-09-01T00:00:00Z at scale 60. A second call returns the same seed, origin, and counts.

Local mode with no Easy Auth principal header is the dev user and is an admin. A present header requires a role claim of `admin`, including in local mode. Azure mode with a missing or non-admin principal is 403. `GET /api/admin/session` is not admin-gated.

## Scenarios

| Id | Class | Signal |
|---|---|---|
| 1 | Engineering change | Edge ring revision C. ESC temperature oscillates on E-02 chamber A. |
| 2 | Equipment out of spec | ESC thermocouple calibration drift. Temperature ramps on E-01 chamber A. |
| 3 | Supply-chain material | Bad precursor gas lot. CF4 flow steps down on E-01 and E-02. |
| 4 | Process drift | Slow chamber seasoning. Etch rate ramps on E-01 chamber A. |

```
SC-1 SC-2 SC-3 SC-4
```

The sample is the first 30 seconds after the baseline origin, so the series does not depend on the wall clock. The same seed produces the same values. A different seed does not. `POST /api/faults` still returns 202 for a free-form body. A parameter that is not on a recipe is recorded and is not applied to the simulator.

## Model pin

`ModelGatewayRegistration` builds the gateway in the API host. `LLM_MODE` stays `replay`, so startup does not need a key. Live calls clone the chat options and set temperature 0 and seed 4242 (`Mps:Model:Seed`, falling back to the demo seed). The prompt hash does not include temperature or the seed.

Scripted replay is off unless `Mps:Model:ScriptedReplay` is true. The key is the scenario id plus the prompt hash. The id comes from chat option `mps.scenarioId`, or from the ambient hint while the scenario request is running. A missing recording directory is an empty catalog. A miss uses the live route. In replay mode there is no live client, so a miss uses the existing replay catalog and then the mock reasoner.

## How it was verified

Before the commit series, on the complete tree: `dotnet format MPS.sln --verify-no-changes --severity warn`, `dotnet build MPS.sln --configuration Release` (0 warnings, 0 errors), and `dotnet test MPS.sln --configuration Release`. Passed 424 tests: Domain 19, ModelGateway 15, Application 167, Agents 77, Architecture 10, Orchestration 15, Connectors 7, MockEnterprise 4, Simulator 54, Contract 39, API 17. `Api.IntegrationTests` still matches no `Category!=PendingLane` tests and exits 0.

Named tests for this round:

- `SameSeedProducesTheSameTelemetry` for each scenario, `ResetRewindsTheClockAndDropsInjectedFaults`, and `CatalogCoversEachRootCauseClass`.
- `LiveCallsArePinnedToTemperatureZeroAndTheConfiguredSeed` and `ScriptedReplayUsesTheScenarioAndPromptHashAndFallsBackToLive`.
- `ResetIsIdempotentAndRestoresTheSeededBaseline`, `ResetRejectsANonAdminPrincipal`, `ScriptedScenarioTelemetryIsStableAndFreeFormFaultsStillAccept`, and `PrincipalRoleIsAdminOnlyWhenTheClaimSaysSo`.

From `web/`: `npm run lint`, `npm test` (27 files, 60 tests), `npm run build`. The production bundle still warns that the main chunk is about 874 kB. The build exits 0.

No browser tools were available. A person has not clicked Reset demo or Run scenario. Vitest (jsdom) and the production build are the check that was run.

Docker Compose was not started. Azure CLI was not used. No git network command was run.

## Open questions for Bob

- Tag `contracts-v1.2` when the addendum looks right. Existing operations are unchanged. Blocking: no.
- `data/replay/scripted` has no recordings. Scripted replay stays off. The scenario id is available on the chat options, and on the ambient hint only while that scenario request runs. A later investigation request does not copy the id onto model calls, so a recording is not used for investigation wording unless the caller sets `mps.scenarioId`. Say if the last scenario id should stay until the next reset. Blocking: no. Live wording may vary slightly either way. That limit is in the reproducibility note under `docs/demo`.
- The reset gate is one process. Two API instances do not share it.
