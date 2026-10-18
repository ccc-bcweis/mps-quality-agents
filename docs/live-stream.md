# Live stream (fleet hub)

The console reads live data from the SignalR hub at `/hubs/fleet` instead of polling. It never makes up readings: a chart, tile or tool diagram with no data says **Waiting for readings…**.

Each series keeps `windowPoints` readings (600, from `data/metadata/simulation.yaml`). The hub pushes only the new points. The chart's x axis stays that wide: new readings enter on the right and old ones scroll off the left. The harness status, the scenario picker, and the speed follow `runningScenarioId`, speed, and stride on the `clock` message. When nothing is running the status line reads **Live**.

## Subscriptions

Every connection joins the `fleet` group. A page asks for more:

| Hub method | Group | Who calls it |
|---|---|---|
| `SubscribeTool(toolId)` / `UnsubscribeTool` | `tool:{id}` | Tool page |
| `SubscribeSite(siteId \| "all")` / `UnsubscribeSite` | `site:{id}` | Fleet and Sites pages |

`LiveSubscriptions` counts subscriptions per connection and forgets a connection when it drops. `LiveStreamPublisher` ignores telemetry for tools that no page watches, and does nothing while no page watches anything or while a demo reset is running.

## Messages

| Message | Group | Body |
|---|---|---|
| `toolLive` | `tool:{id}` | `ToolLiveDelta`: latest readings, status, recipe, and only the **new** points per series. `resync: true` means the burst was too big (more than `MaxPushPointsPerSeries`), so reload `/api/tools/{id}/live`. `manual: true` means a printout reading came in, so reload manual readings. |
| `fleetLive` | `site:{id}`, `site:all` | `FleetLiveDelta`: rows only for the tools whose readings changed. |
| `clock` | `fleet` | Simulated time, every `ClockIntervalSeconds`. |
| `resetProgress` | `fleet` | The reset progress snapshot. |
| `resync` | `fleet` | `{ reason }`: reload everything (sent when a reset finishes). |
| `alertRaised`, `alertCleared`, `ticketOpened`, `ticketChanged`, `incidentChanged`, `investigationChanged`, `capaChanged`, `capaAdvanced` | `fleet` | The changed record (or, for `alertCleared`, `{ id, toolId, clearedAt }`). The client patches its list by id; it does not refetch. |

Pushes go out at most once every `PushIntervalMilliseconds` (default 1000) per flush, with one trailing flush.

## SPC

`SpcRuleSubscriber` listens for `telemetry.reading`. The plant tick writes frames, then `TelemetryPublisher` publishes them. A manual sample uses that same publish (`PlantSimulation.Ingest`, then the pending flush). Prime history is not published, so it is not evaluated. The subscriber scores each enabled reading with the recipe window (`WindowAt` / `Score`) and hands it to the in-memory rule engine. The alert store and this hub are touched only when the rule state changes: raise, escalate, a new rule on the same alert, or clear. `alertRaised` and `alertCleared` are unchanged.

An alert-tier signal asks the business rules for a ticket. A watch does not. Past the per-tool hourly cap the alert still raises, a `ticket.capped` event is written, and intake does not open another ticket. One investigation starts per incident. Clearing is the hysteresis in `spc-rules.yaml` (8 readings inside ±2σ and no live rule). A watch also expires after 24 quiet readings or 30 simulated minutes. Startup, a hard reset, a soft reset, scenario stop, and a fault clear rebuild the engine from active alerts and start the rings empty. A reset does not mark alerts cleared. `Spc:Mode` = `Shadow` counts the same raise, clear, and ticket decisions and writes no alert. The drawing is `docs/diagrams/spc-alert-flow.md`.

## Client

`web/src/live/fleetHub.ts` owns the connection and re-subscribes after a reconnect. `web/src/live/useLive.ts` has the hooks (`useToolLive`, `useFleetLive`, `useSimClock`, `useRecordPatches`, `useLiveResource`). A page reloads once after a reconnect or a `resync`, and polls every 15 s (`FALLBACK_POLL_MS`) **only while the hub is disconnected**.

## Requests per minute, Tool page

- Before: live, manual readings and alerts each polled every 2.5 s (about 72 requests/min), plus the shell's 15 s fleet poll (4/min), plus three plant-wide list refetches on every record event.
- After, connected: 0 polling requests. One load of each resource when the page opens; the server pushes at most one `toolLive` per second and a `clock` every 5 s.
