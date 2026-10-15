# Lane GB-SIM report

Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

Issue #107. Branch `lane/sim`. Commits are local. Nothing was pushed.

| Step | Commit | What landed |
|---|---|---|
| Plant tick | `b8214ed` | In-process tick over every tool in `data/fleet`, seeded telemetry, fault shapes on the stream, SPC alerts from the stream, reset back to nominal. |
| Scenarios | `18cbe19` | SC-5, SC-7, and SC-8, evidence overlays, presenter cards, resolution ranking for bearing wear, a recipe edit, and a shared precursor lot. |
| Live API | `ae7cc93` | `GET /api/fleet/live`, `GET /api/tools/{toolId}/live`, optional `?siteId=` on the list routes, `siteId` on the records. OpenAPI stays 1.3.0. |
| Console | `566eea5` | Fleet and tool pages poll the live trace. A scenario opens the tool with a card and a streaming banner. The click does not stage an alert. |

## Simulation

`SimulationTickService` advances the shared plant every 2 wall seconds by default (`Mps:Simulation`). One wall second is one simulated minute. The ring keeps about four wall minutes of decimated points and primes about ten simulated minutes of nominal history when the tick is enabled. The same seed (default 4242) rebuilds the same trace. Tests that host the API turn the tick off and pump the clock themselves.

A fault from `POST /api/faults` or `POST /api/injector/scenarios/{id}` is registered at the simulator clock. The stream then carries the step, ramp, oscillation, or intermittent offset, in sigma or percent. Western Electric rules run on that stream and open the alert, the ticket, and the investigation. The old single synthetic point is gone. Reset clears faults and starts again from nominal. Agent telemetry and SPC tools read this same store. The scenario evidence board stays active for the run.

The tick covers Northwater, Ironwood, and Lumen, including D-03, I-D-01, and P-D-01. List routes take an optional `siteId`. Leaving it off keeps the original lists. `siteId` is present on the records and is not required in the contract.

## Scenarios

The picker order is SC-1, SC-2, SC-3, SC-4, SC-5, SC-7, SC-8. There is no SC-6.

| Id | Story | Stream |
|---|---|---|
| SC-5 | Precursor lot MX4419 is thin on D-03 (Northwater) and I-D-01 (Ironwood). Notice SN-MX4419 names both plants. A chamber clean on D-03 is a confounder. | Film thickness steps about 8 percent. |
| SC-7 | Foreline pump bearing hours are past the service interval on D-02. | Pump vibration ramps about 5 sigma over about 2.5 real minutes. |
| SC-8 | An operator edited the RF power setpoint on E-03 with no engineering change order. | RF bias power steps about 4.5 sigma on E-03 only. |

SC-2, SC-4, and SC-7 ramp in over about two real minutes, so the fleet tile, the schematic, and the SPC chart move before the rule fires. SC-5 and SC-8 are steps, so the offset is present on the first tick after the scenario is accepted. The POST itself still does not open the alert.

The resolution prompt is 1.3.0. Bearing hours past the interval rank as equipment. A recipe setpoint edit with no engineering change order ranks as process drift. A shared lot with a clean on only one tool keeps the lot as the cause and the clean as a confounder.

Each scenario has a card in `data/scenarios/catalog.yaml`: what is happening, what the sensors do, what the AI should find (collapsed on the harness), and the expected cause. `GET /api/injector/scenarios` returns those fields. The harness dropdown opens the card under the bar. The card uses the full row, wraps long words, and links edge ring, foreline pump, and precursor.

Choosing Run scenario goes to `/tools/{first tool}?streaming=1` and says the trace is streaming. An SPC rule opens the alert when the points cross the limit. The same path is used for a manual inject and for preset S4.

While the live tool list is empty, fleet tiles keep the teaching-floor stories (D-01 alarm, E-02 watch). A non-empty live snapshot replaces only the tools that have samples, so a running tick moves the tiles.

## How it was verified

- `dotnet format MPS.sln` made no further edits.
- `dotnet build MPS.sln`: 0 warnings, 0 errors.
- `dotnet test MPS.sln`: Domain 19, ModelGateway 15, Application 176, Agents 80, MockEnterprise 4, Connectors 7, Orchestration 15, Simulator 63, Architecture 14, Contract 39, Api 27. All passed. Api.IntegrationTests is filtered with `Category!=PendingLane` and had no tests left to run.
- `web/`: `npx tsc --noEmit` exited 0. `npm test` passed 32 files, 87 tests.
- Engine tests cover the same seed twice, a fault that moves only its series, an SPC alert that comes from the stream, and a reset that returns to nominal.
- Web tests cover an empty live list leaving the teaching tiles in place, a live reading turning D-02 to alarm, a streaming etch-rate series on E-01, clone sensors for D-03, scenario cards for SC-4 and SC-5, and navigation to the tool trace with no staged correlation id.

This session had no browser. Widths 1280, 1600, and 1920 were not measured in a window. The scenario card is `basis-full`, `min-w-0`, `max-w-full`, and `break-words`, and the scenario select stays `w-80` with `max-w-full`. jsdom does not measure clipping.

## Notes for Bob

1. With the tick enabled, fleet tiles follow the live stream. The D-01 alarm and E-02 watch stories remain for an empty live list, which is what the tests and a disabled tick use.
2. The SC-5 film step is about 2.4 sigma on the recipe window, so rule 2 opens the ticket. Rule 1 does not fire at this magnitude.
3. Pump vibration is emitted on the oxide recipe, so the SC-7 series is present for part of each recipe cycle.
4. A reset that overlaps an in-flight SPC publish can still insert one alert after the clear. The tests run one step at a time.
