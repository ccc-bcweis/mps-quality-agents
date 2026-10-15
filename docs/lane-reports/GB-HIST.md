# Lane report: history lists

Branch `lane/hist`. Local commits only. Nothing was pushed.

Copyright (c) 2026 Bryan C. Weis and Common Consulting Company. MIT.

Issue 108. Alerts, tickets, investigations, and corrective actions list every past record, newest first.

## Done

| Issue | Commit | What |
|---|---|---|
| 108 | `c2d9b32` | `GET /api/investigations`, newest first. |
| 108 | `1b1a16b` | Optional `siteId` on the alert, ticket, investigation, and corrective-action lists. An unknown plant returns an empty list. |
| 108 | `822e09c` | The four console tabs open those lists. A missing link falls back to the list with a short note. The console never shows the API missing-record title. A scenario run in the browser mock adds the new alert, ticket, and investigation. |
| 108 | `b7a2a6e` | Playwright walks a scenario, then every console tab and every harness link, at 1280, 1600, and 1920. |
| 108 | `3cc137a` | The contract addendum no longer uses an acronym the glossary does not define. |

`67e8fd3` was the partial screen work saved when the earlier run stopped. `822e09c` kept that work and finished it.

Investigation and Approval open `/investigations` and `/approvals`. They do not open a hard-coded investigation id. The API title for a missing record stays "No such record." The screens replace it with notes such as "That investigation is no longer available. Here are all investigations."

A scenario does not create a corrective action. That still waits for a human approval, as the escalation gate requires. The corrective-action tab loads and shows the empty state.

The harness bar, the console frame, and the investigation detail panels were not edited.

## How it was verified

`dotnet build MPS.sln` succeeded with 0 warnings and 0 errors.

`dotnet test MPS.sln` then passed: Domain 19, ModelGateway 15, Application 176, MockEnterprise 4, Agents 77, Simulator 56, Connectors 7, Orchestration 15, Architecture 14, Contract 40, API 26. Failed 0. `Api.IntegrationTests` matched no `Category!=PendingLane` tests and the run exited 0.

`dotnet format MPS.sln --verify-no-changes` exited 0.

From `web/`: `npm test` passed 32 files and 82 tests. `npx tsc --noEmit` exited 0. `npx tsc -p e2e/tsconfig.json --noEmit` exited 0. `npm run e2e` passed 3 tests in system Edge (1280, 1600, and 1920). The walk found the new scenario records on Alerts, Tickets, Investigation, and Approval, found no error text, and found no clipped or overlapping cells in the history tables.

Azure CLI was not used. No git network command was run.

## Open questions for Bob

- The demo fleet tools are all at Northwater, so an Ironwood or Lumen filter is an empty list until those tools are in the catalog. Blocking: no.
- The browser walk uses the Vite mock. A scenario there creates the alert, ticket, and investigation. The service default still does not open a ticket from the injector unless that switch is on. Blocking: no.
- History-table cells were checked for clipping and overlap. The harness bar and console frame were left untouched for the other lane. Blocking: no.
- Corrective actions stay empty until someone approves a recommendation. Blocking: no.
