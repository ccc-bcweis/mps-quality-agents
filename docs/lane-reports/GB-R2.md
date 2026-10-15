# Lane report: GB-3 round 3

Branch `lane/r2-reset`. Issue #83. Local commits only. Nothing was pushed.

Copyright (c) 2026 Bryan C. Weis and Common Consulting Company. MIT.

Detail is in `docs/lane-reports/GB-3-r3.md`. Reproducibility is in `docs/demo`. The contracts v1.2 addendum is in `docs/adr/ADR-001-contracts.md`.

## Done

| Issue | Commit | What |
|---|---|---|
| R2 (#83) scenarios and clock | `11638a1` | Four named scenarios, one per root-cause class. Same seed, same telemetry. Reset rewinds the simulated clock and drops injected faults. |
| R2 (#83) model pin | `49ea218` | Live calls use temperature 0 and seed 4242. Optional scripted replay falls back to live on a miss. |
| R2 (#83) admin reset | `098153b` | `POST /api/admin/reset` requires role `admin` (local dev user when the Easy Auth header is absent). OpenAPI 0.3.0 adds the admin and scenario paths. Free-form `POST /api/faults` still returns 202. |
| R2 (#83) console | `ae7f15d` | Admin-only Reset demo in the header menu, with a confirmation of what is cleared. Fault Injector scenario picker plus the free-form form. |
| Reproducibility | `b8150aa` | Live model wording may vary slightly. |

## How it was verified

Before those commits: `dotnet format MPS.sln --verify-no-changes --severity warn`, `dotnet build MPS.sln --configuration Release` (0 warnings, 0 errors), `dotnet test MPS.sln --configuration Release`. Domain 19, ModelGateway 15, Application 167, Agents 77, Architecture 10, Orchestration 15, Connectors 7, MockEnterprise 4, Simulator 54, Contract 39, API 17. Failed 0. `Api.IntegrationTests` matched no `Category!=PendingLane` tests and exited 0.

From `web/`: `npm run lint`, `npm test` (27 files, 60 tests), `npm run build`. The main chunk is still about 874 kB. The build exits 0.

No browser tools were available. Vitest (jsdom) covers the confirmation dialog and the scenario run. A person has not clicked through the console.

Docker Compose was not started. Azure CLI was not used. No git network command was run. Tool schematic files and `deploy/infra` were not edited.

## Open questions for Bob

- Tag `contracts-v1.2` when the addendum looks right. Blocking: no.
- No scripted-replay recordings are checked in, and the flag defaults to false. The scenario id is not copied onto later investigation model calls. Say if it should stay until the next reset. Blocking: no. Live wording may vary slightly. See the reproducibility note under `docs/demo`.
