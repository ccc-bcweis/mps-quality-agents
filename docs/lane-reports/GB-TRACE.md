# Lane GB-TRACE report

Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

Issues #126 and #133. Branch `lane/bv2-trace`. Commits are local. Nothing was pushed.

| Step | Commit | What landed |
|---|---|---|
| Seasoning | `ec18284` | Chamber seasoning stays process drift. A document-only engineering note and an in-spec lot no longer steal the answer. Replay regression stores that decision. |
| Bus chain | `4b9dfce` | Scenarios SC-1 through SC-8 and a manual fault publish as commands. Readings stream in. The process chart fires from the stream. The business rule opens the ticket and the investigation starts on that chain. |
| Panel | `933c352` | An evidence item in an investigation, an alert, or a ticket opens a plain-language cause panel. |

## Causal trace

`GET /api/trace/{eventId}` is unchanged. The panel reads that route. Ancestors run from the root (a scenario start, a fault command, or an upload) to the focus. The focus tree continues through readings, the process-chart alert, the business rule that fired, the ticket, the investigation steps, and the evidence item.

Each row shows the server summary, a local time, the tool's plain name, and the value against its limits when both limits are present. A burst of readings collapses to one line, "Readings streamed in," with the count and one sample value. Event ids and event types stay inside a Technical details expander. A missing source id says the chain is not on the bus yet. A missing event says the chain was not found.

The button lives on the shared evidence pack, so the same panel also opens from a corrective action and from an approval.

## Scenarios on the bus

`PlantCommandSubscriber` applies `command.scenario.start` and an unscripted `command.fault.inject`. The scenario trigger and the fault route publish the command. They do not apply the plant a second time. A scripted fault inside a scenario is applied once. SC-6 is the exception: the scenario subscriber skips it, and the oscilloscope printout remains the injection.

Percent faults in the catalog sample body stay the physical percent. The live stream steps those faults by 4.5 sigma so a high process-chart alert can fire. The rule that matches hangs between the alert and the ticket. The investigation command hangs from the ticket, and the started step hangs from that command.

Catalog causes still resolve on that chain: SC-1 engineering change, SC-2 calibration, SC-3 and SC-5 supply-chain material, SC-4 process drift, SC-7 equipment, SC-8 process change (ranked as process drift, per `data/scenarios/catalog.yaml`).

## Chamber seasoning

SC-4 is a falling etch rate after a clean on Etch 01. The ranker keeps that as process drift when the only engineering note is a document and the cited lot is in spec. The replay test stores the resolution decision and serves it with no network model. The replayed claim is process drift, the cause names the clean, and it does not cite the document-only engineering note as the change that happened.

## How it was verified

- `dotnet format MPS.sln`: exit 0, no diff.
- `dotnet build MPS.sln`: Build succeeded, 0 warnings, 0 errors.
- `dotnet test MPS.sln` (the project filter `Category!=PendingLane` left in place): Domain 22, Bus 6, ModelGateway 15, Application 204, Agents 87, MockEnterprise 4, Simulator 64, Connectors 13, Orchestration 15, Architecture 15, Contract 40, Api 49. All passed (534). Api.IntegrationTests had no tests left to run.
- `web/`: `npm test` passed 42 files and 123 tests. `npx tsc --noEmit` exited 0.
- Playwright `web/e2e/causal-trace.pw.ts` passed at 1280, 1600, and 1920 (3 passed, 16.4s). The open panel had no clipped or overlapping labels. The timeline shows chuck temperature, the value against the limit, and the business rule. The event id stays in Technical details.
- API chain test walks each scenario from the command through readings, the alert, the matched rule, the ticket, and the investigation, and checks the trace summary for the rule event.
- Agent replay test covers the seasoning decision.

## Notes for Bob

1. Sync is still `sync_to_github.ps1`. Nothing here was pushed. This lane left the harness bar and the reset path alone for the controls lane.
2. The envelope, the command catalog shape, the event log, the telemetry table, the trace response, and the scenario and fault HTTP bodies are unchanged. OpenAPI stays 1.4.0. No incident-table column was added.
3. An escalated investigation still cannot be plain-approved.
4. A percent scenario's HTTP sample is still the physical percent. The running plant steps that fault at 4.5 sigma so the process chart can open a ticket.
5. SC-6 still enters through the printout. The scenario command does not apply that fault a second time.
6. The evidence pack is shared, so corrective actions and approvals open the same cause panel.
