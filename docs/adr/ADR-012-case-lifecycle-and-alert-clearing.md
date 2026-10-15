# ADR-012: Case lifecycle, alert clearing and stopping a scenario

Status: Accepted (2026-10-15, Bryan Weis)

## Context
Injected faults lasted until a reset. Alerts had no status, so they never cleared. Closing a corrective action (CAPA)
did not close the incident or ticket, and nothing tied the CAPA stages to the plant.

## Decision
1. Approval creates the CAPA (unchanged).
2. Moving the CAPA to **Containment** removes every injected fault on the incident's tool, so its telemetry returns to
   nominal. A `fault.cleared` event is written to the trace on the incident's correlation id.
3. Alerts have a status: `active` or `cleared`. An alert clears on its own after **8 readings in a row** inside
   +/-2 sigma of the center, all after the alert was raised. An `alert.cleared` event is written to the trace. Repeat
   alerts on the same signal are still held back for 10 minutes.
4. Moving the CAPA to **Verification** is refused (409) while any alert on the incident's tool, chamber and parameter is
   still active.
5. Closing the CAPA closes the incident, and so its ticket. The investigation keeps its Approved verdict.
6. The harness has **Stop scenario** (`POST /api/injector/stop`), which removes all injected faults. Records stay. A
   `command.scenario.stop` event is written. The injector takes an optional duration in minutes; the fault ends that
   long after onset.

The lifecycle rules are listed in the Rules table, and the future-state process map shows containment, verification
and close.

## Consequences
- A full case can be walked from alert to closed CAPA in the console without a reset.
- Verification depends on the alert clearing, so the CAPA cannot reach Verification while the fault is still running.
- Containment clears every fault on the tool, not only the one that raised the incident. That is acceptable because
  the plant runs one case per tool at a time.
- Alert status is stored in SQLite with the other records and, like them, is wiped on redeploy.
