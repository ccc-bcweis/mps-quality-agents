# Lane report: GB-4 round 4 (web)

Branch `lane/4d-schematic`. Local commits only. Nothing was pushed. Contract files, `data/fleet`, `data/glossary.yaml`, and `design/brand/logo-concept-1.svg` were not changed.

Copyright (c) 2026 Bryan C. Weis and Common Consulting Company. MIT.

Issue UX2 (#60). The Tool Detail schematic loads the sensor catalog nested under each tool in `data/fleet` at build time, the same way the glossary loads. `web/src/fleet/sensors.fallback.json` is deleted. Vite fails the build when `data/fleet` is missing or parses to zero sensors.

| Issue | Commit | What landed |
|---|---|---|
| UX2 (#60) | `1972bb1` | Four family drawings (deposition, etch, strip, clean) on a 1600×1000 view. Every `schematicAnchor` in the catalog is a faceplate on the SVG, and the SVG has no extra anchor. A test fails in either direction. Each faceplate shows the mock value, the catalog unit, and a status color from that sensor's spec limits. Glossary `Term` marks acronyms on the faceplate. A click opens that point's SPC chart. Clean tools have no RF generator and no match. |

Catalog size, both tools in a family share one anchor set:

| Family | Sensors per tool | Hardware on the drawing |
|---|---|---|
| Deposition | 28 | Chamber, showerhead, heater thermocouple, three mass-flow controllers (SiH4, N2O, NH3), RF forward and reflected with a match, pressure gauge, throttle, pump vibration, load lock and wafer handler, thickness, uniformity, particles, wafer counter (run rate, throughput, output, yield). |
| Etch | 38 | Chamber, showerhead, ESC and edge ring, wall thermocouple, four mass-flow controllers (CF4, CHF3, O2, Ar), source and bias RF (forward and reflected) with a match, pressure gauge, throttle, pump vibration, load lock and wafer handler, etch rate, endpoint, particles, wafer counter. |
| Strip | 24 | Chamber, remote plasma, platen thermocouple, two mass-flow controllers (O2, N2H2), remote RF forward and reflected with a match, pressure gauge, throttle, pump vibration, load lock and wafer handler, ash rate, particles, wafer counter. |
| Clean | 14 | Bath, spray bar, spin chuck, megasonic, chemical manifold (SC1, SC2, DIW), ambient pressure gauge, drain pump and vibration, cassette and chuck runout, particles, residue, wafer counter. Chamber A only. |

Mock telemetry (`web/src/fleet/telemetry.ts`) is what the faceplates read and what MSW returns from `GET /api/tools/{toolId}/telemetry`. Ids and units match the catalog for D-01, D-02, E-01, E-02, S-01, S-02, C-01, and C-02. The page does not fetch that route. See `docs/design-questions/GB-4-r4.md`.

Pitch samples on the drawing:

- D-01 chamber A pressure is 6.52 Torr. The spec maximum is 6, so the faceplate is out of spec.
- E-02 chamber A ESC temperature is 53.1 C. The warning band is 42–58 and the spec is 40–60, so the faceplate is in spec. The story tile above the drawing still says "53.1 °C osc" and WATCH, because that tile is the Western Electric rule 2 story, not the spec-limit color.
- Strip remote plasma and clean megasonic use catalog watts. The story tiles still say "3.1 kW" and "720 W". Those tiles are the authored story in `web/src/fleet/catalog.ts`. The faceplates use the catalog unit W and a sample inside the warning band (strip remote nominal 2800 W, clean megasonic nominal 700 W).

Chamber A keeps the tool's authored SPC chart when the chosen sensor's `parameter` is that chart's parameter. Other points get a chart labeled from the anchor, with the catalog unit. Choosing E-02 chamber A pressure opens "SPC chart of Chamber pressure".

Strip tools in the demo tool list now name Chamber A and Chamber B, matching the catalog. API chamber ids stay C1 and C2.

## How it was verified

Before `1972bb1`, from `web/`: `npm run lint`, `npm test` (26 files, 57 tests), `npm run build`. From the repo root: `dotnet format MPS.sln --verify-no-changes`, `dotnet build MPS.sln --configuration Release` (0 warnings, 0 errors), `dotnet test MPS.sln --configuration Release --no-build`.

Release tests: Domain 19, ModelGateway 13, Application 167, Simulator 47, Agents 77, Orchestration 15, MockEnterprise 4, Connectors 7, Architecture 10, Contract 39, Api 13. Api.IntegrationTests reported no tests matching `Category!=PendingLane` and the run still exited 0.

Vite's production build warns that the main chunk is about 870 kB (recharts plus the embedded sensor catalog). Lint, tests, and the build still exit 0.

No browser tools were available. The dev server was not started, and a person has not clicked through Tool Detail. Vitest (jsdom) clicks the E-02 chamber pressure faceplate and checks the chart, and the production build typechecks the page. That is the check that was run.

## Notes for Bob

- No contract change. The open telemetry question is in `docs/design-questions/GB-4-r4.md`. Blocking: no. A root `DESIGN_QUESTIONS.md` was not added, so this lane does not collide with another lane's copy of that filename.
- The schematic status comes from spec limits. The E-02 ESC faceplate is in spec at 53.1 C while the tile stays WATCH for rule 2. Say if the faceplate should follow the tile instead.
- Story tiles for strip remote plasma and clean megasonic still use the old pitch strings. The drawing uses catalog watts.
