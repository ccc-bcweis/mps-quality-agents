# Lane report: H1

Branch `lane/h1`. Local commits only. Nothing was pushed.

Copyright (c) 2026 Bryan C. Weis and Common Consulting Company. MIT.

Detail is in `docs/lane-reports/H1.md`. The decision is `docs/adr/ADR-006-demo-harness.md`.

## Done

| Issue | Commit | What |
|---|---|---|
| H1 harness vs application | `c3b6c25` | StatusNeo demo harness bar (navy and gold) above the Meridian Precision Systems console. Fault injector, return on investment, and metrics are harness routes. Reset demo, the scenario picker, and the live / replay / mock spend line stay on the bar. Console tabs are Fleet, Tool, Alerts, Tickets, Investigation, Approval, and CAPA, with a Sites slot before Fleet. |
| H1 docs | `530463d` | ADR-006, wireframes, `docs/demo/SCREENS.md`, and the deck screenshot plan in `docs/PLAN.md`. |

## How it was verified

From `web/`: `npm run lint` (clean), `npm test` (28 files, 69 tests passed), `npm run build` (exit 0; main chunk 881.95 kB, existing size warning).

Headless Edge at 1280 and 1920: harness band above the console, no clipped or overlapping labels on the bar or the console tabs, injector opens a harness panel, reset confirmation still cancels, scenario SC-4 lands on Alerts inside the console, `/roi` redirects to `/harness/roi`. Confirm reset was not clicked. Tool, Tickets, Investigation, Approval, CAPA, Metrics, and Glossary were not clicked in the browser; Vitest covers those pages.

Docker Compose was not started. Azure CLI was not used. No git network command was run. Tool schematic files, `src/`, `infra/`, and `deploy/` were not edited.

## Open questions for Bob

- Sites belongs at `CONSOLE_TABS` index 0 when lane S2 lands. Blocking: no.
- Glossary stays a console header link. Say if it should be a tab. Blocking: no.
- In-page injector shortcuts were removed from Fleet, Alerts, and Tickets. The tickets channel label still says Fault injector. Blocking: no.
- Old `/injector`, `/roi`, and `/metrics` paths redirect. Blocking: no.
- Return-on-investment heading still leads with "ROI". Blocking: no.
- Scripted-replay recordings are still absent (lane R2). Blocking: no.
- StatusNeo artwork is still the placeholder. Blocking: no.
