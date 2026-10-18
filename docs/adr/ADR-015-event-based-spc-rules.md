# ADR-015: SPC rules run on every reading on the bus

Status: Accepted (2026-10-18, Bryan Weis)

Supersedes, in part, ADR-012's clearing path. Containment still removes the fault. It does not clear the alert. The rule engine does, once later readings satisfy the hysteresis in `data/metadata/spc-rules.yaml`.

## Context

SPC used to poll plant memory beside the bus. Full Western Electric and Nelson rules ran only on series with an injected fault. Every other series was pre-filtered at 3 sigma, so a drift inside the control limits never reached rules 2, 3, 4, or the trend rules. Clearing depended on an in-memory watch list. A soft reset, a process restart, or the end of that watch dropped the list, and an alert that was still active was never evaluated again.

The approved design is the opposite. Every sensor reading is a `telemetry.reading` on the bus. A subscriber evaluates that reading for every enabled sensor. Alerts raise and clear only from rule state. That state is rebuilt from the alert store on startup and after every reset, so an alert that was active is still evaluated when nominal readings arrive.

## Decision

1. `SpcRuleSubscriber` (`IHostedService`) subscribes to `telemetry.reading`. The plant tick and a manual sample both reach it through `TelemetryPublisher`. The prime written into the chart window is not published and is not evaluated.
2. `SpcRuleEngine` keeps one series per tool, chamber, and parameter: a fixed ring and incremental counters. Each reading is O(1). The store is written only when the state changes (raise, escalate, rule-set change, or clear).
3. The engine scores with the same recipe-step window the charts use (`WindowAt` / `Score`). Rules are the spec window, Western Electric 1–4, and Nelson runs of N rising and N falling. A new alert takes the reading's correlation id. A missing id, or one that starts with `nominal-`, becomes `spc-` plus a new id.
4. Tiers, hysteresis, debounce, the count-rule magnitude, sensor-kind enablement, watch expiry, and the per-tool hourly ticket cap are loaded from `data/metadata/spc-rules.yaml` and rejected at startup when invalid. Shipped values, and why:
   - Western Electric 1 (beyond 3σ) and 2 (2 of 3 beyond 2σ), and the spec window, are **alert**. Rule 2 is what catches a drift that never reaches 3σ and still opens a ticket.
   - Western Electric 3 (4 of 5 beyond 1σ), rule 4 (8 on one side), and both Nelson trends (6 rising, 6 falling) are **watch**. A watch updates the alert and does not open a ticket.
   - Rule 4 and both Nelson trends require a mean offset of **1σ** over the run. Simulator noise sits near 0.2σ, and an 8-in-a-row rule on an exact centre fires about once per 128 readings. A 1σ floor keeps a healthy hour at zero watch raises and zero alert raises. A real drift of about 1σ or more still fires.
   - Clearing stays **8 readings inside ±2σ** with no live rule. A watch also expires after **24 readings** or **30 simulated minutes** with no new violation, whichever comes first. An alert tier does not expire on time; it clears only through that hysteresis.
   - Debounce stays **10 minutes**. The ticket cap is **6 per tool per simulated hour**. Past the cap the alert still raises, a `ticket.capped` event is written, and intake does not open another ticket. A hard or soft reset restarts the plant clock and the cap.
   - `sensors.enabledKinds` is `periodic`. Cumulative counters are not evaluated. `disabledParameters` can skip a named parameter as well.
   - `limitCheck.recentReadings` (20) and `recoveryWatchMinutes` (30) stay in the file as the numbers Rules & Process already shows. The engine does not use them as a gate or a watch list.
5. Nominal, watch, and alert are one alert per signal. Escalation updates that alert and then asks the business rules for a ticket. The investigation still starts once per incident.
6. On startup, and on `command.reset.hard`, `command.reset.soft`, `command.scenario.stop`, and `fault.cleared`, the engine is rebuilt from active alerts. Rings start empty. Nothing in SPC reads injected-fault state, and nothing clears an alert just because a reset ran. A hard reset has already deleted the rows, so the rebuild sees an empty store. A soft reset keeps the rows.
7. `Spc:Mode` is `Live` or `Shadow` (`appsettings.json`). Shadow evaluates and counts what it would raise, clear, and ticket. It writes no alert.

## Consequences

- A drift inside 3σ is visible to rules 2, 3, and 4. Healthy noise does not open alert-tier signals, and with the 1σ floor it does not open watches either.
- An alert left active by a soft reset or a restart clears when later readings satisfy the rules. The old watch list is gone.
- Only state changes hit SQLite and SignalR. The rule math itself is cheap at the 10× reading rate.
- The batch evaluator (`WesternElectricEvaluator`) uses the same thresholds, including the magnitude gate, so a series can be checked both ways. The live path is the incremental engine.
- The ARCH D plotter sheets are hand-composed and were not redrawn. The review drawing is `docs/diagrams/spc-alert-flow.md`.
