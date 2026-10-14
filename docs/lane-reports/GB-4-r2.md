# Lane report: GB-4 round 2 (web)

Branch `lane/4b-ux`. Local commits only. Nothing was pushed. Contract files, `data/`, and `ci.yml` were left as they were. The web job already runs `npm ci`, lint, test, and build when `web/package.json` exists.

Copyright (c) 2026 Bryan C. Weis and Common Consulting Company. MIT.

SPEC §2.10 and ADR-003 are the product rule for this round. The data lane owns `data/glossary.yaml` and the sensor catalog. This lane owns rendering. Where those files are absent, the console loads a stub in `web/` and says so on the page.

| Issue | Commit | What landed |
|---|---|---|
| UX1 (#59) | `a60e7c7` | Glossary-driven `Term`: dotted underline, hover and focus tooltip (portaled, so it stays out of the closed control's text), spell-out on first use of an acronym on each screen. Glossary page at `/glossary`. Build loads `data/glossary.yaml` when that file exists, otherwise `web/src/glossary.fallback.json`. A Vitest scan fails when a UI string literal contains an acronym that is missing from the loaded glossary. |
| UX2 (#60) | `c059693` | Tool Detail draws one SVG per family (deposition, etch, strip, clean) with subsystem marks and sensor buttons. Each button shows the reading, the unit, and a status color, and a click swaps the SPC chart to that point. Anchors come from `schematicAnchor`. The current `data/fleet` files have no `sensors:` list, so the build uses `web/src/fleet/sensors.fallback.json` and prints that note under the drawing. E-02 `endpoint.time` is left off the drawing on purpose, and the page says it is not on the drawing. |
| UX3 (#61) | `7c0547c` | Investigation page shows the agent-step timeline (triage, the four cause classes, evidence tool calls, evidence gate, escalation, ranking, grounding, CAPA draft, human approval, mock QMS) with queued / running / done / escalated chips. Status follows `investigation.stage`. The page reloads the investigation every 5 seconds. Each evidence item shows its why-it-matters line. A root-cause matrix marks supports / rules out for S1 and S2. |
| UX4 (#62) | `9f5c5bc` | Fleet and Tool Detail show run rate, uptime, OEE, first-pass yield, MTBF, and MTTR with glossary tooltips. The fleet row is the average of the eight tools (26.4 wafers/h, 94.0% uptime, 78.4% OEE). Each tile shows that tool's run rate and OEE beside the link, so the link text still carries ALARM on D-01 and WATCH on E-02. Every block is labeled "Illustrative demo readings, not a live tool counter." |

## How it was verified

Before each commit above, from `web/`: `npm run lint`, `npm test`, `npm run build`. From the repo root: `dotnet format MPS.sln --verify-no-changes`, `dotnet build MPS.sln --configuration Release` (0 warnings, 0 errors), `dotnet test MPS.sln --configuration Release --no-build`.

Web suite on the UX4 commit: 24 files, 48 tests. That includes the acronym scan, the glossary page, the four family schematics and the E-02 chart swap, the S1/S2 timeline and matrix, and the fleet and E-02 metric strings. Release tests: Domain 19, ModelGateway 13, Application 158, Simulator 36, Agents 58, Orchestration 14, MockEnterprise 4, Architecture 10, Connectors 7, Contract 39, Api 11. Api.IntegrationTests reported no tests matching `Category!=PendingLane` and the run still exited 0. That filter result was already true on this branch before this round.

Vite's production build warns that the main chunk is about 786 kB (recharts). Lint, tests, and the build still exit 0.

No browser tools were available. The dev server was not started, and a person has not clicked through Fleet, Tool Detail, the Glossary, or an investigation. Vitest (jsdom) and the production build are the check that was run.

## Notes for Bob

- Open questions are in `docs/design-questions/GB-4-r2.md`. None block the pitch. Round 1 notes stay in `docs/design-questions/GB-4.md`.
- First use is the page body. Nav, the lifecycle bar, the mode banner, approval buttons, and uppercase sensor labels keep the short form (`spellOut` false) so their accessible names stay stable. The tooltip still opens.
- `deploy/web.Dockerfile` is still the placeholder that copies `deploy/web/index.html`. It does not run the Vite build. This lane stayed inside `web/` and the existing CI web job.
- The acronym scan reads string literals in `web/src` (tests and the API/mock client files are skipped). JSX text nodes are outside that scan. A new mixed-case token has to be listed in the scan and in the glossary together.
