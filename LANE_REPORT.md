# Lane report: GB-4 round 4

Branch `lane/4d-schematic`. Local commits only. Nothing was pushed.

Copyright (c) 2026 Bryan C. Weis and Common Consulting Company. MIT.

Detail is in `docs/lane-reports/GB-4-r4.md`. The telemetry contract question is in `docs/design-questions/GB-4-r4.md`.

## Done

| Issue | Commit | What |
|---|---|---|
| UX2 (#60) | `1972bb1` | Tool Detail loads the real `data/fleet` sensor catalog at build time. `web/src/fleet/sensors.fallback.json` is deleted. Deposition, etch, strip, and clean each have an instrumented schematic. Every catalog `schematicAnchor` has a matching SVG anchor, and the SVG has no extra anchor. Each faceplate shows value, catalog unit, and in-spec / warning / out-of-spec color. A click opens that sensor's SPC chart. MSW telemetry uses the same sensor ids and units. Clean tools have no RF generator. |

No contract files were changed.

## How it was verified

From `web/`, before `1972bb1`: `npm run lint`, `npm test` (26 files, 57 tests), `npm run build`.

From the repo root, before that commit: `dotnet format MPS.sln --verify-no-changes`, `dotnet build MPS.sln --configuration Release` (0 warnings, 0 errors), `dotnet test MPS.sln --configuration Release --no-build`. Domain 19, ModelGateway 13, Application 167, Simulator 47, Agents 77, Orchestration 15, MockEnterprise 4, Connectors 7, Architecture 10, Contract 39, Api 13. Api.IntegrationTests matched no `Category!=PendingLane` tests and the run exited 0.

The production bundle warns that the main chunk is about 870 kB. The build still exits 0.

No browser tools were available. A person has not clicked through Tool Detail. Vitest (jsdom) and the production build are the check that was run.

## Open questions for Bob

- OpenAPI has no telemetry snapshot. The page reads a shared generator, and MSW exposes `GET /api/tools/{toolId}/telemetry` for tests. The page does not call that route. Recommendation: leave the contract for this lane. Blocking: no. Detail is in `docs/design-questions/GB-4-r4.md`.
- E-02 chamber A ESC is 53.1 C, inside the 42–58 warning band, so the faceplate is in spec. The story tile above it stays "53.1 °C osc" and WATCH for Western Electric rule 2. Say if those two should agree.
- The strip remote-plasma tile still says "3.1 kW" and the clean megasonic tile still says "720 W". The faceplates use catalog watts (nominal 2800 W and 700 W) and a sample inside the warning band.
- D-01 chamber A pressure on the drawing is 6.52 Torr and out of spec (maximum 6).
