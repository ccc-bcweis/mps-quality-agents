# Lane the controls lane report

Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

Issues #130, #131, and #132. Branch `lane/bv2-ctrl`. Commits are local. Nothing was pushed.

| Step | Commit | What landed |
|---|---|---|
| Resets | `9c21336` | Hard and soft reset as bus commands. Hard wipes transactional data, keeps metadata, and reseeds a week. Soft closes open work and keeps history. |
| Replay | `b21c72c` | Recorded bus messages for SC-1 through SC-8, and a console swap between Live and one recording. |
| Harness | `a491ed9` | Collapsed harness bar, both resets with progress, plant-data Live or Replay, and the answer engine kept separate. |

## Resets

Both commands go through the bus with a full envelope.

`command.reset.hard` deletes transactional rows (event log, telemetry, alerts, incidents, tickets, investigations, evidence, approvals, corrective actions, uploads, faults, and scenario runs). It does not touch `data/metadata`. It then rebuilds the eight-tool fleet and calls `INominalHistorySeeder.SeedAsync(NominalWindow.LastWeek)`, the window ADR-008 chose. The command event is published after the wipe, so the fresh log still contains it.

`command.reset.soft` deletes nothing. It closes every open ticket and investigation, and every pending approval, with the status **Closed by demo reset**. That is a real stage on the state machine, with an audit line and a bus event caused by the reset. It clears live faults and scenario overlays, then writes one healthy reading per sensor. History stays on the tickets and investigation lists.

ADR-008 says a soft reset keeps the log and the telemetry table and clears live faults. This lane also closes open work, so a presenter can return every tool to nominal without throwing away the story. The accepted ADR was not rewritten.

The harness polls `GET /api/admin/reset/progress` while the reset runs. The confirmation dialog closes as soon as the presenter confirms, so the console stays usable. There is no blocking panel.

## Replay

Live follows the running plant. Replay shows one scenario's recorded bus messages from `data/replay/<scenario>/messages.jsonl`. Those files were recorded by `tools/ReplayRecorder` from a real run. Saved model answers stay in `data/replay/scripted/` and are a different switch (saved answers versus a live model).

Switching the console to Live restores the running plant view and leaves the recording in place. A banner on the console says the numbers on screen are the recording. Fleet tiles, tool charts, and alerts follow the recording. Tickets stay the live history.

## Harness

The bar starts collapsed: a down arrow and one line for plant data, the active scenario, and the answer engine. Opening it shows scenario cards, the fault injector, a sample printout upload, hard reset, soft reset, plant data Live or Replay, the answer engine, return on investment, metrics, and the audit log. Each browser stores `expanded` or `collapsed` under `mps.harness`. A missing value stays collapsed.

The open controls scroll inside a 190px region. At 1280 by 800 the whole bar measured at most 267 pixels, about a third of that screen. The console scrolls on its own.

## How it was verified

- `dotnet format MPS.sln` made no further edits. `dotnet build MPS.sln`: 0 warnings, 0 errors.
- `dotnet test MPS.sln`: Domain 22, Bus 6, ModelGateway 15, Application 215, Agents 81, MockEnterprise 4, Orchestration 15, Architecture 15, Simulator 64, Connectors 13, Api 52. Api.IntegrationTests had no tests left under `Category!=PendingLane`. Contract failed one mock-coverage test because the record-scenario route had no handler; that handler was added and the test was re-run and passed. The other 39 contract tests had already passed.
- `web/`: `npm test` passed 41 files and 122 tests. `npx tsc --noEmit` exited 0.
- Playwright at 1280, 1600, and 1920: the history walk, the harness labels, and the ticket printout layout. The open bar stays within a third of a 1280 by 800 screen. No clipped or overlapping harness labels on those widths.
- Reset tests check that metadata is untouched, that a hard reset refills telemetry, and that a soft reset keeps history and uses the status Closed by demo reset. Replay tests swap the console to a recording and restore Live. Web tests cover harness collapse across a remount.

## Notes for Bob

1. Soft reset closes open tickets, investigations, and pending approvals. It does not delete them. ADR-008 still describes the log and telemetry half of that command.
2. Plant-data replay and the answer engine are different switches. Do not point a presenter at `data/replay/scripted/` for the console recording.
3. The harness starts closed. The presenter opens it with the down arrow. The choice sticks in that browser.
4. The escalation gate is unchanged.
5. This lane did not edit the investigation screens or scenario evidence beyond the one close-status check already in the reset commit. GB-TRACE owns those screens.
6. Sync is still `sync_to_github.ps1`. Nothing here was pushed.
