# Lane GB-BUS report

Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

Issues #125, #126, and #129. Branch `lane/bv2-bus`. Commits are local. Nothing was pushed.

| Step | Commit | What landed |
|---|---|---|
| Envelope | `3042981` | In-process bus, per-tool order, bounded queues that wait instead of dropping. |
| Rewire | `f752113` | Commands, telemetry, alerts, tickets, investigation steps, approvals, and corrective actions go through that bus. Trace route, event log, telemetry table, nominal-history seeder, fleet hub. |
| Measurement | `d7d5ad4` | Measured week and ADR-008. In-process is enough for the demo. Production keeps the same envelope on Event Hubs and Service Bus. |

## Event path

One bus is the entry point. The record is documented in `docs/architecture/event-envelope.md` and `contracts/events/envelope.schema.json`. The bus assigns the event id (a ULID), the time it was recorded, and a sequence that starts at 1 inside one correlation id. The payload is JSON.

Commands are listed in `data/commands/catalog.yaml` (kind, payload, who may issue, events emitted). The existing routes stay thin and keep their response shapes. Each service publishes its command after its own checks. A local caller with no identity header may issue every command. A hosted non-admin may not issue a system command. Reset still refuses a non-admin before that check.

Readings go to a telemetry table indexed by tool, parameter, and time. They are not copied row by row into the event log. Alerts, tickets, investigation steps, approvals, corrective actions, and commands are appended to the event log. Indexes cover correlation, causation, and tool plus time.

## Cascade

A fault is one root and many downstream events. The fault command is the parent and the cause of the readings it shifts. The process-control alert hangs from the triggering reading. The ticket hangs from the alert. The investigation command hangs from the ticket, and each later step hangs from the step before it. Evidence items store the event id they were derived from. The approval hangs from the recommendation, and the corrective action hangs from the approval.

`GET /api/trace/{eventId}` returns the ancestors back to the root, then a tree rooted at the focus. Each node has a plain-language summary, the tool's plain name, when it happened, and the value against its limits when the fact is a reading. Unknown ids return 404. The web client can call it through `getTrace`. No page renders the tree yet. OpenAPI stays 1.3.0.

The fleet hub at `/hubs/fleet` subscribes to the bus and pushes fleet, telemetry, alert, ticket, and investigation updates. Polling stays as the fallback. An approval grant is recorded on the bus and is not a separate hub signal.

## History

`INominalHistorySeeder.SeedAsync(NominalWindow.LastWeek)` is what other lanes should call. The 2026-10-15 run stored 32 tools and 574 parameters: 466,662 rows in 7.02 seconds (66,442 rows per second). The last hour is one point every 25 seconds. Older points are one every 900 seconds. Those strides are constants. Chart reads for one tool and one parameter, warm cache, were 0.036, 0.060, and 0.237 milliseconds at the 95th percentile for 15 minutes, 1 hour, and 7 days. The database file plus its write-ahead log was 131,798,200 bytes.

Full-rate totals were projected and not inserted: 2,066,400 rows for an hour, 49,593,600 for a day, and 347,155,200 for a week. A one-second last hour does not fit the 20-second seed target (about 37 seconds at the measured insert rate). Details are in `docs/perf/bus-throughput.md` and `docs/adr/ADR-008-event-bus.md`.

## How it was verified

- `dotnet format MPS.sln`, then `dotnet build MPS.sln`: 0 warnings, 0 errors.
- `dotnet test MPS.sln` (the project filter `Category!=PendingLane` left in place): Domain 22, ModelGateway 15, Bus 6, Application 178, Agents 81, Orchestration 15, Architecture 15, MockEnterprise 4, Connectors 13, Simulator 64, Contract 40, Api 45. All passed. Api.IntegrationTests had no tests left to run.
- `web/`: `npm ci`, then `npm test` passed 38 files and 115 tests. `npx tsc --noEmit` exited 0.
- Bus tests cover envelope fields, per-tool order, and back-pressure that does not drop an alert.
- API tests cover a fault cascade from the command through the corrective action, a missing trace id, and hub messages for a fault and for an operator report.
- The throughput test publishes the pinned week and checks the seed finishes within a minute and the chart reads stay under half a second. The measured chart reads are far under that.

This lane did not change a page. Widths 1280, 1600, and 1920 were not checked in a browser. There is no new label to clip.

## Notes for Bob

1. The demo bus stays in-process. Production telemetry should go to Event Hubs, and commands and domain events to Service Bus topics. The envelope does not change. ADR-008.
2. Seed the pitch history with `NominalWindow.LastWeek`. Do not insert the projected full-rate week. The last hour is 25 seconds, not one second, because the one-second hour misses the seed-time target.
3. A hard reset clears the event log and the telemetry table, then rebuilds the original eight-tool nominal plant. A soft reset clears live faults and keeps tickets and the log. The seeder clears telemetry only.
4. The escalation gate is unchanged. An escalated investigation still cannot be plain-approved. The stand-in investigation text is unchanged. Resolution prompts are unchanged.
5. This lane did not edit `data/fleet`, `data/metadata`, or the Sites and Rules pages.
6. Sync is still `sync_to_github.ps1`. Nothing here was pushed.
