# SPC alert flow

**Copyright (c) 2026 Bryan C. Weis and Common Consulting Company**

How a reading becomes a watch, an alert, a ticket, or a clear. The rules and the numbers are `data/metadata/spc-rules.yaml` (ADR-015). This is a review drawing. The ARCH D sheets under `print/` are hand-composed and are not regenerated from this file.

## Diagram

```mermaid
flowchart TD
  tick["Plant tick or manual sample"] --> pub["TelemetryPublisher: telemetry.reading"]
  pub --> sub["SpcRuleSubscriber"]
  sub --> enabled{"Kind and parameter enabled?"}
  enabled -->|no| ignore["Not evaluated"]
  enabled -->|yes| score["Score with the recipe window"]
  score --> engine["SpcRuleEngine: spec window, WE 1-4, Nelson rising and falling"]
  engine --> changed{"Rule state changed?"}
  changed -->|no| quiet["No alert write"]
  changed -->|yes| mode{"Spc:Mode"}
  mode -->|Shadow| ledger["Count the raise, clear, or ticket. Write nothing"]
  mode -->|Live| store["SpcAlertService writes the alert"]
  store --> tier{"Alert tier, and the business rules open a ticket?"}
  tier -->|watch, or no| alertOnly["Alert only. One row per signal"]
  tier -->|yes| cap{"Under the per-tool hourly cap?"}
  cap -->|yes| ticket["Intake ticket. One investigation per incident"]
  cap -->|no| capped["Alert stays. ticket.capped. No ticket"]
  clear["Eight in-control readings, or a watch expires"] --> store
  rebuild["Startup, hard reset, soft reset, scenario stop, fault clear"] --> seed["Rebuild from active alerts. Rings start empty"]
  seed --> engine
```

## Narrative

The plant loop no longer runs a second SPC pass over memory. Publishing the reading is what evaluates it. A manual sample is ingested into the same pending list and published as the same event.

The engine keeps a short ring and a few counters per series. It does not scan the 600-point chart window. SQLite and the fleet hub are updated only when the series moves: a new watch or alert, an escalation on that same alert, a change to the set of live rules, or a clear.

Clearing does not read injected-fault state. After a reset the rings are empty and any alert that is still stored is the series state. Later nominal readings clear it through the hysteresis. A hard reset has already deleted the alerts, so there is nothing to rebuild. Shadow mode walks the same decisions and records the counts.
