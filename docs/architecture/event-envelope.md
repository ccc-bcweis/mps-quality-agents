# Event envelope

Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

One bus is the entry point for telemetry, system commands, and application commands. A reading, an alert, a ticket, an investigation step, an approval, a corrective action, and a command are the same record with a different type. The schema is `contracts/events/envelope.schema.json`. It is not one of the twelve lifecycle documents in `contracts/events/event.schema.json`. Those stay the closed catalog for the older event names.

## Fields

| Field | Meaning |
|---|---|
| eventId | Identifier assigned by the bus. 26 characters, Crockford alphabet, sortable by time. |
| type | Dotted name. Examples: `telemetry.reading`, `alert.raised`, `alert.cleared`, `ticket.created`, `ticket.capped`, `investigation.step`, `approval.granted`, `capa.created`, `command.fault.inject`, `command.scenario.start`, `command.reset.hard`, `command.reset.soft`, `document.uploaded`. |
| correlationId | One cascade. A fault command and everything it causes share it. |
| causationId | The event that caused this one. |
| parentEventId | Where this event hangs in the trace tree. A fan-out (many readings from one fault) uses the fault id as both parent and cause. |
| sequence | Monotonic inside one correlation id. The bus overwrites whatever the publisher sent. |
| source | Publisher name, such as `injector`, `simulator`, or `investigation`. |
| siteId, toolId, chamberId | Plant, tool, and chamber. Chamber may be empty. |
| occurredAt | When the fact happened. |
| recordedAt | When the bus accepted it. |
| schemaVersion | Starts at 1. |
| payload | A JSON object. Null becomes `{}`. |

`GET /api/trace/{eventId}` walks parent links, then causation, across the event log and the telemetry table. Ancestors are listed root-first and do not include the focus. The tree is rooted at the focus. Each node has a plain-language summary, the tool's plain name, and the value against its limits when the fact is a reading.

## How a cascade links

1. `command.fault.inject` is the root.
2. Each affected reading uses that command as parent and cause.
3. `alert.raised` uses the triggering reading as parent and cause.
4. `ticket.created` hangs from the alert.
5. `command.investigation.start` hangs from the ticket. Each `investigation.step` hangs from the step before it. Evidence records the reading id it was derived from.
6. `command.approval.grant` hangs from the recommendation. `approval.granted` hangs from that command. `capa.created` hangs from the approval.

`telemetry.reading` carries `kind` (`periodic` or `cumulative`, or `periodic` when the sensor does not say). The SPC subscriber listens only to `telemetry.reading`, `command.reset.hard`, `command.reset.soft`, `command.scenario.stop`, and `fault.cleared`. A new alert uses the reading's correlation id. A correlation that is missing or starts with `nominal-` becomes `spc-` plus a new id. `ticket.capped` is written when the hourly ticket cap is already full: the alert still raises, and intake does not open another ticket. The payload is `signal` = `ticketCapped`, plus `toolId`, `parameter`, `at`, `cap`, and `opened`.

Commands are listed in `data/commands/catalog.yaml` (kind, payload, who may issue, events emitted). The HTTP handlers stay thin: check the caller, then the existing service publishes the command after its own checks. The response shapes are unchanged. The caller gets a correlation id back where the route already returned one.

## Demo transport

The demo bus is in-process, including the hosted container. Each tool has a bounded queue. A full queue waits. Alerts are not dropped. A tool keeps its own order. Telemetry for one tool can move as a batch. Readings are stored in a telemetry table `(tool, parameter, time)`. Each sensor keeps the newest 600 readings (`data/metadata/simulation.yaml`, `telemetryWindow.readingsPerSensor`) in that table and in memory. Older readings roll off. A reset does not seed a week of history onto the bus. The plant primes healthy readings into memory and does not publish that prime as bus events. Other facts go to an append-only event log. ADR-008 records whether this is enough for the pitch and what production would use instead. The envelope does not change between the two.
