# Lane report: GB-4 round 5 (web)

Branch `lane/4e-schematic-fit`. Local commits only. Nothing was pushed. Contract files, `data/fleet`, `data/glossary.yaml`, and `design/brand/logo-concept-1.svg` were not changed.

Copyright (c) 2026 Bryan C. Weis and Common Consulting Company. MIT.

Issue UX2 (#60), Tool Detail schematic fit. The previous drawing was one 1600×1000 canvas inside `min-w-[1600px]` and `overflow-x-auto`. Chamber B sat 660px to the right of Chamber A, so a normal window clipped that column. Bryan treats a clipped or overlapping label as a defect. From 1280px to 1920px the drawing now stays inside the window: no clipped panel, no overlapping faceplate, no horizontal scroll on the schematic, and sensor text stays at 11px or larger.

| Issue | Commit | What landed |
|---|---|---|
| UX2 (#60) | `50c3f3f` | The schematic is separate panels, each with its own viewBox and local coordinates. A shared load-lock panel, one panel per chamber (clean tools use their stations), and a tool-counter card row. Chamber A and Chamber B share the same geometry. CSS grid wraps the chambers. Sensor buttons are a percent of their own panel. |

Panels, in order:

| Panel | Who | Coordinates |
|---|---|---|
| Load lock and wafer handler | Every family. Capped at 700px wide. | Dual-chamber tools place center-offset sensors for A and B here. Clean tools draw the cassette and handler only. Chuck runout stays on the bath station. |
| Chamber A, Chamber B | Deposition, etch, strip. | Same 700px-wide viewBox. Etch is 836px tall. Deposition and strip are 640px tall. |
| Bath and spin chuck, Chemical manifold | Clean only. | Bath view is 400px tall. Manifold view is 540px tall. |
| Tool counters | Run rate, throughput, output, yield. | Wrapping cards. Minimum card width 220px. An aria-hidden SVG still holds the counter anchors so the catalog match covers them. |

The chamber and station grid is `repeat(auto-fit, minmax(min(100%, 620px), 1fr))` with a 12px gap. Two chambers need 1252px in that row. Tool Detail is the only route whose shell is `max-w-[1680px]`. Every other route stays `max-w-[1200px]`. Padding on the shell and the panel is 16px each side. At a 1280px layout width the chambers stack, and each panel is still wider than 620px. Side by side starts once the schematic row reaches 1252px, which is about a 1320px browser window after padding and a scrollbar. At 1920px the shell is 1680px, the schematic row is about 1616px, and each chamber is about 802px. The load-lock panel stays at 700px.

Sensor buttons keep `data-anchor`, `data-sensor-id`, and `data-status`. The label and the sensor id are 11px. The value is 12px. The value and the id use `break-words`. A label that is wider than the button wraps onto a second line. The buttons have no truncate class. `faceplateProblems` checks the longest catalog id and value for each anchor at 620px (chamber or station), 700px (load lock), and 220px (counter card). A 200px etch panel is required to report a fit problem, so that check is not empty.

`CHAMBER_SHIFT` and the shared 1600×1000 view are removed. The schematic markup has no `min-w-[1600px]` and no `overflow-x-auto`. The console nav and the data tables on other pages still scroll sideways. Those were left as they were.

## How it was verified

Before `50c3f3f`, from `web/`: `npm run lint`, `npm test` (26 files, 61 tests), `npm run build`. From the repo root: `dotnet format MPS.sln --verify-no-changes`, `dotnet build MPS.sln --configuration Release` (0 warnings, 0 errors), `dotnet test MPS.sln --configuration Release --no-build`.

Release tests: Domain 19, ModelGateway 13, Application 167, Simulator 47, Agents 77, Orchestration 15, MockEnterprise 4, Connectors 7, Architecture 10, Contract 39, Api 13. Api.IntegrationTests reported no tests matching `Category!=PendingLane` and the run still exited 0.

Schematic tests, per panel: every catalog `schematicAnchor` matches a drawn `[data-schematic-anchor]` and the drawing has no extra anchor; both tools in a family share one anchor set; `overlappingPairs` is empty; `anchorsOutsideView` is empty at the viewBox edge and at an 8px inset; Chamber A and Chamber B have the same local geometry; hardware captions stay inside the viewBox and off the faceplates.

Vite's production build warns that the main chunk is 872.05 kB. Lint, tests, and the build still exit 0.

No browser tools were available. The dev server was not started, and a person has not clicked through Tool Detail at 1280px or 1920px. The width check is the grid formula above plus the faceplate fit test. Vitest (jsdom) still clicks the E-02 chamber pressure faceplate and checks the chart.

## Notes for Bob

- No contract change this round. A root `DESIGN_QUESTIONS.md` was not added. The telemetry question from round 4 is still open and still non-blocking: OpenAPI has no telemetry snapshot. Detail is in `docs/design-questions/GB-4-r4.md`.
- E-02 chamber A ESC is 53.1 C, inside the 42–58 warning band, so the faceplate is in spec. The story tile stays "53.1 °C osc" and WATCH for Western Electric rule 2. Say if those two should agree.
- The strip remote-plasma tile still says "3.1 kW" and the clean megasonic tile still says "720 W". The faceplates use catalog watts (nominal 2800 W and 700 W) and a sample inside the warning band.
- Stacking under about 1320px is intentional. A stacked etch chamber is tall (view 700×836) and the page scrolls vertically. Say if Tool Detail should stay side by side at 1280px. That would need a chamber minimum under 620px, and the 11px text would need another pass.
