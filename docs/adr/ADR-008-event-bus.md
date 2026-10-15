# ADR-008: Event bus

Status: Accepted for this demo build (issues #125, #126, and #129, recorded by lane GB-BUS) — 2026-10-15
Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

## Context

Every telemetry record, alert, ticket, investigation step, approval, corrective action, and command has to enter through one bus. A later reader must be able to walk from one root command to the readings, the alert, the ticket, the investigation, and the corrective action.

The spec already names the production target: Event Hubs for telemetry, and Service Bus for commands. Locally it says to queue on an in-process channel. This note records which of those the pitch should run, using a measured seed rather than a guess. The envelope is `contracts/events/envelope.schema.json` and is described in `docs/architecture/event-envelope.md`.

## Decision

The demo keeps one in-process bus. Each tool has a bounded queue (capacity 512). A full queue waits. Alerts are not dropped to make room. A tool keeps its own order. Telemetry for one tool can move as a batch. Readings are stored in a telemetry table. Other facts go to an append-only event log. The trace route reads both.

The seeded history other lanes should call is `INominalHistorySeeder.SeedAsync(NominalWindow.LastWeek)`. On the 2026-10-15 run that call stored seven days for 32 tools and 574 parameters: 466,662 rows in 7.02 seconds (66,442 rows per second). The last hour is one point every 25 seconds. Older points are one every 900 seconds. Those strides are constants. Chart reads for one tool and one parameter, warm cache, were 0.036, 0.060, and 0.237 milliseconds at the 95th percentile for 15 minutes, 1 hour, and 7 days. The database file plus its write-ahead log was 131,798,200 bytes. Full-rate totals (one row per second) were projected and not inserted: 2,066,400 rows for an hour, 49,593,600 for a day, and 347,155,200 for a week. The measurement is in `docs/perf/bus-throughput.md`.

A one-second last hour was not seeded. That hour alone is 2,066,400 rows. Together with the older week it would be about 2.45 million rows and about 37 seconds at the measured insert rate, which misses the 20-second seed target. The pinned history meets the seed-time and chart-time targets. It is not the production sample rate. An earlier draft let the stride follow a short timing sample. A quiet run picked these same strides. A run during the full test suite would have stretched older points to 1,800 seconds. The stride no longer moves with machine load.

Production, when this leaves the pitch laptop, should publish telemetry to Event Hubs and commands and domain events to Service Bus topics. The envelope stays the same record. Only the transport changes. The in-process bus remains the local stand-in.

## Rationale

The measured week is enough for the pitch. Seeding finishes in well under 20 seconds, and a chart window for one parameter stays well under 100 milliseconds. Live fault cascades are a handful of events on top of that, not another week of one-second rows. The bus already keeps per-tool order and waits instead of dropping an alert.

A full-rate week is a different problem. 347 million rows, at the measured size of about 282 bytes per stored row, is on the order of 98 billion bytes before anyone asks for a second plant or a longer retention. That is an Event Hubs workload. Commands, approvals, and corrective actions are fewer, must not be lost, and need a competing-consumer topic. That is Service Bus. Mixing them into one telemetry stream would make the cascade harder to read, which is the thing the envelope is for.

Sizing the demo history from a short sample, then downsampling the older days, keeps the laptop seed repeatable. Other lanes call the same seeder instead of inventing a second history.

## Consequences

- `GET /api/trace/{eventId}` and the SignalR hub subscribe to this bus. Polling stays only as a fallback.
- A hard reset clears the event log and the telemetry table. A soft reset keeps both, and clears live faults.
- Replacing the transport later must not change the envelope fields, the causation links, or the command catalog in `data/commands/catalog.yaml`.
- Do not insert the projected full-rate week into the demo database. Call `SeedAsync` with `NominalWindow.LastWeek`.
- A 5-second last hour would be about 797,000 rows and about 12 seconds at the measured insert rate. It was not adopted. The 25-second hour already meets the seed and chart targets. A later lane that wants that finer hour should remeasure before changing the constants.
