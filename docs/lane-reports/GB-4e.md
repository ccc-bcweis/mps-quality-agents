# Lane report: GB-4 round 5

Branch `lane/4e-schematic-fit`. Local commits only. Nothing was pushed.

Copyright (c) 2026 Bryan C. Weis and Common Consulting Company. MIT.

Detail is in `docs/lane-reports/GB-4-r5.md`. The telemetry contract question from round 4 is still in `docs/design-questions/GB-4-r4.md`.

## Done

| Issue | Commit | What |
|---|---|---|
| UX2 (#60) | `50c3f3f` | Tool Detail no longer clips Chamber B. The schematic is a load-lock panel, one panel per chamber (clean tools use bath and chemical-manifold stations), and a tool-counter card row. Each panel has its own viewBox. Chambers wrap in a CSS grid at 620px. Sensor text stays at 11px or larger, and labels wrap. The 1600px horizontal scroll box is removed. |

No contract files were changed.

## How it was verified

From `web/`, before `50c3f3f`: `npm run lint`, `npm test` (26 files, 61 tests), `npm run build`.

From the repo root, before that commit: `dotnet format MPS.sln --verify-no-changes`, `dotnet build MPS.sln --configuration Release` (0 warnings, 0 errors), `dotnet test MPS.sln --configuration Release --no-build`. Domain 19, ModelGateway 13, Application 167, Simulator 47, Agents 77, Orchestration 15, MockEnterprise 4, Connectors 7, Architecture 10, Contract 39, Api 13. Api.IntegrationTests matched no `Category!=PendingLane` tests and the run exited 0.

The production bundle warns that the main chunk is 872.05 kB. The build still exits 0.

No browser tools were available. A person has not clicked through Tool Detail. Vitest (jsdom), the faceplate fit test, and the grid formula are the check that was run. At 1280px the chambers stack. Side by side starts around a 1320px window. At 1920px each chamber is about 802px inside a 1680px tool-detail shell.

## Open questions for Bob

- OpenAPI has no telemetry snapshot. The page reads a shared generator, and MSW exposes `GET /api/tools/{toolId}/telemetry` for tests. The page does not call that route. Recommendation: leave the contract for this lane. Blocking: no. Detail is in `docs/design-questions/GB-4-r4.md`.
- E-02 chamber A ESC is 53.1 C, inside the 42–58 warning band, so the faceplate is in spec. The story tile above it stays "53.1 °C osc" and WATCH for Western Electric rule 2. Say if those two should agree.
- The strip remote-plasma tile still says "3.1 kW" and the clean megasonic tile still says "720 W". The faceplates use catalog watts (nominal 2800 W and 700 W) and a sample inside the warning band.
- D-01 chamber A pressure on the drawing is 6.52 Torr and out of spec (maximum 6).
- Chambers stack below about 1320px so a 1280px window does not scroll sideways. A stacked etch chamber is tall. Say if 1280px must stay side by side. That needs a chamber minimum under 620px and another text-fit pass.
