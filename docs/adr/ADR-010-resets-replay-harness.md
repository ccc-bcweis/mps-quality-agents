# ADR-010: Demo resets, replay data swap and the collapsible harness

Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

**Status:** Accepted, 2026-10-15 (Baseline v2, issues #130, #131, #132). **Decided by:** Bryan Weis (decisions t157u), recorded by the architect.

## Context
The requirements baseline (section 5) asks for two reset levels, a replay mode that swaps the console's data, and a harness bar that no longer takes a third of the screen.

## Decision
1. **Hard reset** is the bus command `command.reset.hard`. It purges every transactional table (event log, telemetry, alerts, incidents, tickets, investigations, evidence, approvals, CAPA, uploads, faults, scenario runs). It never touches metadata (ADR-009). It then seeds nominal history with `INominalHistorySeeder` using `NominalWindow.LastWeek` (ADR-008: seven days, one point every 25 s for the last hour and every 900 s before that, about 467k readings in about 7 s).
2. **Soft reset** is `command.reset.soft`. It deletes nothing. Open tickets, investigations and pending approvals move to the distinct status **Closed by demo reset**, with an event and an audit entry. Faults and scenario overlays clear, and fresh nominal telemetry brings every tool back to nominal.
3. While a reset holds its gate, the plant tick pauses, and the reset first drains in-flight investigations. Without this, a reading or ticket from the previous run could land in the middle of a reset. That race explained an SC-4 run that raised no alert.
4. **Replay** is a data swap. Recorded bus messages for each scenario live in `data/replay/<scenario>/messages.jsonl` (the recorder steps the plant so the stream is captured). In Replay, the console reads that recorded set behind a banner, and Live restores the live stream. The **AI engine** switch (saved or live answers) is separate and does not change plant data.
5. **Harness bar** is collapsed by default to one row with a down arrow and a one-line status. Expanding it shows scenarios, the fault injector, both resets, replay or live, the AI engine, ROI and metrics. The state is stored per browser in localStorage.

## Consequences
Every reset and replay switch is a command on the bus with a full envelope, so it appears in the causal trace and the audit log. A redeploy still starts from an empty SQLite file. The first hard reset reseeds it, which is intended for a thin demo.
