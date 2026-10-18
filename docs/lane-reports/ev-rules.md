# Lane report: ev-rules

Branch `lane/ev-rules`, off `50ced27`. Local commits only. Nothing was pushed.

Copyright (c) 2026 Bryan C. Weis and Common Consulting Company.

## What changed

SPC no longer polls plant memory. `SpcRuleSubscriber` listens for `telemetry.reading`. Every enabled reading is scored with the recipe window (`WindowAt` / `Score`) and folded into `SpcRuleEngine` (one ring and incremental counters per tool, chamber, and parameter). The alert store is written only when the rule state changes. Alerts raise and clear only from that state. Startup, hard reset, soft reset, scenario stop, and fault clear rebuild the engine from active alerts and start the rings empty. A reset does not mark alerts cleared.

| Commit | What |
|---|---|
| `49d3b2e` | Incremental engine, yaml knobs, magnitude gate, ticket cap, shadow ledger, `ticket.capped`. |
| `bd0e26c` | Bus subscriber, publisher kind, reset rebuild, shadow option. Removes `StreamSpcPublisher`. |
| `dfa1a98` | Regression tests, shadow hour, golden keys for the new settings. |
| `72080ff` | SPEC, live stream, architecture, diagram, ADR-015. ADR-012 notes the partial supersession. |

### Files

- `src/Mps.Application/Spc/SpcRuleEngine.cs`, `SpcEvaluation.cs`, `SpcShadowLedger.cs`, `SpcTicketCap.cs` (new)
- `src/Mps.Application/Spc/SpcAlertService.cs`, `SpcAlertModels.cs`, `SpcRuleSettings.cs`, `SpcRuleNames.cs`, `SpcRuleText.cs`, `SpcSummary.cs`, `WesternElectricEvaluator.cs`
- `src/Mps.Api/Bus/SpcRuleSubscriber.cs` (new), `TelemetryPublisher.cs`
- `src/Mps.Api/Live/SpcOptions.cs` (new), `SpcCoverage.cs`, `SimulationTickService.cs`
- `src/Mps.Api/Live/StreamSpcPublisher.cs` (deleted)
- `src/Mps.Api/Demo/DemoResetService.cs`, `src/Mps.Api/Hosting/QualityHost.cs`, `src/Mps.Api/appsettings.json`
- `data/metadata/spc-rules.yaml`
- `tests/Application.Tests/Spc/SpcRuleEngineTests.cs` (new), `SpcAlertServiceTests.cs`, `WesternElectricEvaluatorTests.cs`
- `tests/Api.Tests/SpcShadowTests.cs` (new), plus the API tests that used to call the old publisher
- `tests/Api.Tests/Golden/effective-limits.golden.json` (new setting keys only; the Linux telemetry hash is unchanged)
- `docs/SPEC.md`, the testing guide, `docs/live-stream.md`, `docs/architecture/data-model.md`, `docs/architecture/event-envelope.md`
- `docs/diagrams/spc-alert-flow.md` (new), `docs/diagrams/README.md`, `investigation-flow.md`, `logical.md`
- `docs/adr/ADR-015-event-based-spc-rules.md` (new), `docs/adr/README.md`, ADR-012, ADR-013

### Touched outside the lane (minimal)

- `src/Mps.Domain/Events/EventTypes.cs`: added `ticket.capped`.
- `src/Mps.Api/Hubs/SignalRQualityNotifier.cs`: an `alertRaised` event is stamped at `LastSeenAt`, so an escalation lines up with the reading that opened the ticket.
- `src/Mps.Application/Printouts/DocumentIntakeService.cs`: a confirmed sample is ingested once. The pending flush publishes it at the observed time. The old extra bus publish used the wall clock and was evaluated a second time.

No SPC code reads `Simulator.ActiveFaults`. `web/` was not edited.

## Design decisions

Shipped `data/metadata/spc-rules.yaml`:

- Control limit 3σ, watch tile 2σ, critical 4σ.
- Western Electric 1 (beyond 3σ) and 2 (2 of 3 beyond 2σ), and the spec window, are alert. Rule 2 is what catches a drift inside 3σ and still opens a ticket.
- Western Electric 3 (4 of 5 beyond 1σ), rule 4 (8 on one side), and Nelson runs of 6 rising and 6 falling are watch. A watch does not open a ticket.
- Rule 4 and both Nelson trends require a mean offset of 1σ. Simulator noise sits near 0.2σ, and an 8-in-a-row rule on an exact centre fires about once per 128 readings. The 0.5σ floor still raised watches on a healthy hour. 1σ clears both shadow hours.
- Clear after 8 readings inside ±2σ with no live rule. A watch also expires after 24 quiet readings or 30 simulated minutes. An alert tier does not expire on time.
- Debounce 10 minutes. Ticket cap 6 per tool per simulated hour. Past the cap the alert still raises, `ticket.capped` is written, and intake does not open another ticket. A hard or soft reset restarts the plant clock, so the cap starts over. Scenario stop and fault clear do not.
- `sensors.enabledKinds` is `periodic`. Cumulative counters are not evaluated. `disabledParameters` can skip a named parameter.
- `limitCheck.recentReadings` (20) and `recoveryWatchMinutes` (30) stay as the numbers Rules & Process already shows. The engine does not use them as a gate or a watch list. Removing them would change the catalog JSON and the web fixture.

`Spc:Mode` is `Live` or `Shadow` (default Live). Shadow counts raises, clears, and tickets it would write, and writes nothing.

A new alert keeps the reading's correlation. A blank id, or one that starts with `nominal-`, becomes `spc-` plus a new id. The subscriber remembers the current reading on the cascade before it writes the alert, so `alertRaised` parents to the reading that changed the rule state.

The batch evaluator still reports each fired rule at its latest triggering point. A Nelson falling run that is no longer true on the last point is left out. Callers treat the earliest violation time as the start of the current excursion, and a finished trend was pulling that window earlier than the rules still in force (SC-4 then classified the etch-rate ramp as intermittent spikes).

## Tests added

`SpcRuleEngineTests`: `TheShippedFileNamesEveryNewKnob`, `AMissingTierFailsLoadWithTheFieldName`, `AFaultCorrelationIsKeptAndANominalOneBecomesAnSpcId`, `IncrementalRulesMatchTheBatchEvaluatorOnTheSameSeries`, `IncrementalSpecWindowMatchesTheBatchEvaluator`, `EachZoneAndCountRuleCatchesADriftInsideThreeSigma`, `EightSmallPointsOnOneSideDoNotFireTheCountRule`, `AWatchExpiresWhenSimulatedMinutesPassWithoutANewViolation`, `RebuildKeepsTheAlertAndEmptiesTheRun`.

`SpcAlertServiceTests`: `AnAlertClearsAfterARestartWhenNominalReadingsArrive`, `ADriftInsideThreeSigmaIsCaughtByWe2We3AndWe4`, `AWatchExpiresAfterQuietReadings`, `TheHourlyTicketCapStillRaisesTheAlert`, `ADisabledParameterIsNotEvaluated`. `SixFallingPointsFireTheFallingNelsonRule` was added on the batch evaluator.

`SpcShadowTests`: `ASimulatedHourOfNominalTelemetryRaisesNoAlertTier`, `ShadowHourAtOneXAndTenXPrintsTheCounts` (trait `Shadow`; returns immediately unless `SPC_SHADOW=1`), `AnAlertClearsAfterASoftResetWhenNominalReadingsArrive`.

Existing API tests that called `StreamSpcPublisher` now drain `SpcRuleSubscriber`. Manual samples still go through `PlantSimulation.Ingest` and the same publisher as sensor frames.

## Shadow hour

Headless plant, no scenario, all tools, one simulated hour. `SpcShadowTests.ShadowHourAtOneXAndTenXPrintsTheCounts` with `SPC_SHADOW=1`. No per-rule lines, because nothing fired.

| Cadence | Stride | Alert raises | Watch raises | Clears | Tickets would-be | Tickets suppressed |
|---|---|---|---|---|---|---|
| 1x | 15 s | 0 | 0 | 0 | 0 | 0 |
| 10x | 1 s | 0 | 0 | 0 | 0 | 0 |

## Checks

`dotnet format --verify-no-changes --severity warn` passed. `dotnet test MPS.sln -warnaserror` compiled with warnings as errors.

Passed 888, failed 2, skipped 1, total 891.

| Project | Result |
|---|---|
| Domain.Tests | 39 passed |
| ModelGateway.Tests | 18 passed |
| Application.Tests | 278 passed |
| Bus.Tests | 6 passed |
| MockEnterprise.Tests | 4 passed |
| Agents.Tests | 150 passed, 1 failed |
| Orchestration.Tests | 25 passed |
| Connectors.Tests | 13 passed |
| Architecture.Tests | 16 passed |
| Simulator.Tests | 94 passed |
| Api.IntegrationTests | 23 passed |
| Contract.Tests | 41 passed |
| Api.Tests | 181 passed, 1 failed, 1 skipped |

The two failures are the known Windows-only ones. `EvalSuiteTests.TheReplayEvalPassesTheGatesAndTheCommittedReportIsCurrent` differs at index 1 (Windows line endings). `EffectiveLimitsGoldenTests.EveryConsumerReadsTheSameValuesAsTheGoldenSnapshot` differs only on `sim.nominalTelemetry.2h.sha256`: golden `1CE889025570CD55EC46C3B29F2B9D55772B39316BD47384915E681B9CFB4D59`, this machine `E4900D3B54663F9C1AB7ED4E0351610C99D0CD04DABE7A07ADF42D96430FF17D`. The committed hash is the Linux one. CI is Linux. The Windows hash was not committed. The skipped test is `LiveModelEvalTests.TheLiveModelPicksTheExpectedClassAndNeverCitesARuledOutDecoy`.

## Open

- `nelson-6-falling` is evaluated and has sentences, and it is not in `SpcRuleText.RuleIds`. The console catalog does not list it. Adding the id would change `web/src/test/catalog.fixture.json`, which this lane does not edit.
- `docs/diagrams/spc-alert-flow.md` is the review mermaid. `docs/diagrams/render.ps1` was not run. The ARCH D sheets under `print/` are hand-composed and were not redrawn.
- `limitCheck.recentReadings` and `recoveryWatchMinutes` remain catalog figures only.
