# Lane report: GB-4 round 3 (web)

Branch `lane/4b-ux`. Local commit only. Nothing was pushed. Contract files were not changed.

Copyright (c) 2026 Bryan C. Weis and Common Consulting Company. MIT.

The console now reads `data/glossary.yaml` as its only glossary. `web/src/glossary.fallback.json` is removed. Vite fails the build if the YAML file is missing.

| Issue | Commit | What landed |
|---|---|---|
| UX1 (#59) fix-up | `a1ba1d7` | Rule codes such as WE-1 use the glossary term WE and keep the numeric suffix. The first rule on a screen spells out "Western Electric rules (WE-1)". A later WE-2 on that screen stays "WE-2". The acronym scan still fails if WE itself is missing. Prose linking is limited to acronyms, so a revision letter such as "rev C" stays a letter. Units and plain words remain on the Glossary page and still open a tooltip when a screen asks for them by id. |
| UX4 (#62) fix-up | `a1ba1d7` | Run rate, Uptime, and First-pass yield are glossary entries, so the fleet tiles keep their labels and tooltips from the shared file. OEE, MTBF, and MTTR already had entries. Tests expect those expansions. |

Terms added to `data/glossary.yaml` (term, expansion, plainDefinition, whyItMatters):

| Term | Expansion |
|---|---|
| First-pass yield | wafers that passed the first time |
| IoT | internet of things |
| MTTR-Q | mean time to resolve a quality incident |
| Run rate | wafers finished per hour |
| SPEC | specification |
| Uptime | share of scheduled time the tool was ready |

MTTR-Q is its own term. The scan does not treat it as a suffix of MTTR. IoT and SPEC are in console copy, so the acronym scan requires them.

## How it was verified

Before `a1ba1d7`, from `web/`: `npm run lint`, `npm test` (24 files, 50 tests), `npm run build`. From the repo root: `dotnet format MPS.sln --verify-no-changes`, `dotnet build MPS.sln --configuration Release` (0 warnings, 0 errors), `dotnet test MPS.sln --configuration Release --no-build`.

Release tests: Domain 19, ModelGateway 13, Application 167, Simulator 47 (includes GlossaryTests), Agents 77, Orchestration 15, MockEnterprise 4, Connectors 7, Architecture 10, Contract 39, Api 13. Api.IntegrationTests reported no tests matching `Category!=PendingLane` and the run still exited 0.

Vite's production build warns that the main chunk is about 811 kB (recharts). Lint, tests, and the build still exit 0.

No browser tools were available. The dev server was not started, and a person has not clicked through Alerts, Fleet, an investigation, or the Glossary. Vitest (jsdom) and the production build are the check that was run.

## Notes for Bob

- No contract change. No `DESIGN_QUESTIONS.md` for this round.
- `data/glossary.yaml` is the data lane's file. This round added the six terms above because the console and the acronym scan need them. Please keep them on merge.
- Spell-out is once per glossary term per screen. WE-1 and WE-2 share WE, so only the first rule code is spelled out.
- Single-letter units (C, W, s) and plain words (wafers, count, percent) are in the glossary and are not auto-linked inside sentences.
