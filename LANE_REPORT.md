# Lane report: GB-4 round 3

Branch `lane/4b-ux`. Local commits only. Nothing was pushed.

Copyright (c) 2026 Bryan C. Weis and Common Consulting Company. MIT.

Detail is in `docs/lane-reports/GB-4-r3.md`.

## Done

| Issue | Commit | What |
|---|---|---|
| UX1 (#59) | `a1ba1d7` | The console uses `data/glossary.yaml` as its only glossary. `web/src/glossary.fallback.json` is removed. WE-1 is the term WE plus a rule suffix. The acronym scan still requires WE, and it requires MTTR-Q as its own term. |
| UX4 (#62) | `a1ba1d7` | Run rate, Uptime, and First-pass yield come from the shared glossary. Screen tests expect the real expansions (Western Electric rules, statistical process control as a running check, OEE as availability times speed times good-wafer rate). |

Also added to `data/glossary.yaml`: IoT, SPEC, and MTTR-Q. No contract files were changed.

## How it was verified

From `web/`: `npm run lint`, `npm test` (24 files, 50 tests), `npm run build`.

From the repo root, before the code commit: `dotnet format MPS.sln --verify-no-changes`, `dotnet build MPS.sln --configuration Release` (0 warnings, 0 errors), `dotnet test MPS.sln --configuration Release --no-build`. Simulator.Tests passed 47, including GlossaryTests. Api.IntegrationTests matched no `Category!=PendingLane` tests and the run exited 0.

The production bundle warns that the main chunk is about 811 kB. The build still exits 0.

No browser tools were available. A person has not clicked through the console. Vitest (jsdom) and the production build are the check that was run.

## Open questions for Bob

- `data/glossary.yaml` belongs to the data lane. This round added First-pass yield, IoT, MTTR-Q, Run rate, SPEC, and Uptime. Please keep those entries on merge.
- Prose tooltips attach to acronyms. A revision letter such as "rev C" is not given the Celsius tooltip. Units remain on the Glossary page.
- WE-2 on the same screen as WE-1 stays short, because both codes share the term WE. The suffix stays visible.
- No contract change, so there is no design-question file for this round.
