# ADR-013: Manual readings are samples, not a workflow

Status: accepted, 2026-10-16

## Context
Printout readings (oscilloscope, thermocouple logger and similar) used to have their own path. Confirming one
opened a ticket, raised a `manualMeasurement` alert, injected a fault, and was cleared by special-case
containment. That gave us two sources of truth. In one case the ESC tile kept showing the manual 68 C in
alarm color after the CAPA had closed.

## Decision
- A confirmed reading becomes one `TelemetryFrame` with `Source = manual` and the evidence id. It goes through
  `PlantSimulation.Ingest`, the same function sensor ticks use, into the same series.
- From there the rules engine is the generic one: spec window, Western Electric rules, alert, debounce, ticket,
  investigation, CAPA, containment, and auto-clear. Nothing in it knows about printouts.
- Removed: printout ticket creation, the `manualMeasurement` alert rule, the printout fault injection, and the
  printout investigation start.
- Kept, as presentation only: the Manual badge, the chart marker, the evidence link, the printed time for audit,
  and the Now button.
- `GET /api/manual-readings` reports `active`, which is true while the alert raised from that sample is active.
  The tile overlay and the alarm-colored marker follow `active`. A resolved reading stays as history.

## Generic defects found and fixed
1. `StreamSpcPublisher` checked only the newest point of each series. A single breaking point followed by a
   normal reading was never evaluated. It now checks every point since the last check.
2. A series on the recovery watch list was skipped by the limit-break check for 30 minutes, so a new break
   in that time went unseen.
3. After a clear, an old breaking point that was still in the rule window raised the alert again.
   `SpcAlertService` now ignores violations at or before the newest alert on that signal, so a point alerts once.
4. The series log accepted only appends at the head. A late point is now inserted in time order.

## Consequences
- A reading observed before the live chart window is stored and shown, but the rules do not evaluate it.
  Use Now to stamp it at plant time.
- SC-6 injects its oscillation like every other scenario and adds the oscilloscope sheet as one manual sample.
