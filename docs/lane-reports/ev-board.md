# Lane report: ev-board

Branch `lane/ev-board`, off `50ced27`. Local commits only. Nothing was pushed.

Copyright (c) 2026 Bryan C. Weis and Common Consulting Company. MIT.

## What changed

| Commit | What |
|---|---|
| `5159020` | Evidence board keyed by fault correlation, generic printout step, injector records |
| `a0bc51b` | SPEC and demo notes for that behavior |

Files:

- `src/Mps.Api/Demo/ScenarioEvidenceBoard.cs` — registry keyed by correlation, with `Use`, `Find`, `RegisterScenario`, `RegisterRecords`, `Remove`, `RemoveScenario`, and `Clear`
- `src/Mps.Api/Demo/CorrelationBoundInvestigationAgent.cs` — binds the incident correlation for the evidence gather
- `src/Mps.Api/Demo/ScenarioManualSamples.cs` — printout steps from `data/scenarios/catalog.yaml`
- `src/Mps.Api/Demo/FaultEvidenceCatalog.cs` — fault-catalog enterprise records for an injected fault
- `src/Mps.Api/Demo/ScenarioTrigger.cs` — generic printout step, `RecordOnly` faults, register under each fault correlation
- `src/Mps.Api/Bus/PlantCommandSubscriber.cs` — record-only commands are skipped; stop and fault-cleared drop board entries
- `src/Mps.Api/Live/SeedEnterpriseTools.cs` — overlay from `CurrentCorrelation` only
- `src/Mps.Api/Live/LiveModelHost.cs` — wraps the investigation agent with the correlation binding
- `src/Mps.Api/Hosting/QualityHost.cs` — registers the printout catalog, the board, and the fault-evidence catalog
- `src/Mps.Application/Faults/FaultInjectionService.cs` — `RecordOnly`, and optional evidence on the fault correlation
- `src/Mps.Application/Faults/FaultEvidenceAttachment.cs` — records, merge, and the board and catalog ports
- `src/Mps.Api/Http/QualityEndpoints.Injector.cs`, `ApiContracts.cs`, `FaultEvidenceBody.cs` — `evidence` and `evidenceFaultId` on `POST /api/faults`
- `data/scenarios/catalog.yaml` — SC-6 printout `oscilloscope-e01.pdf` at timing `now`
- `contracts/openapi.yaml` — those two optional fault fields
- `tests/Api.Tests/ScenarioEvidenceTests.cs`, `EvidenceBoardTests.cs`, `PrintoutEndpointTests.cs`
- `tests/Application.Tests/Faults/FaultInjectionServiceTests.cs`
- `docs/SPEC.md` (section 2.1 injector paragraph and the SC-6 row in 2.7), `docs/demo/REPRODUCIBILITY.md`, `docs/demo/DEMO-SCRIPT.md`

`ScriptedScenarioContext` stays. It only tells model replay which scenario is being applied during that request. `DemoResetService` still calls `Clear()`.

## Design decisions

- A scenario start publishes one correlation, and every fault in that scenario is registered under it. The trigger registers the scenario id and the plant time. The subscriber applies the stream and does not pick a single active scenario.
- An injected fault may send records, name a fault-catalog id, or both. Catalog rows come first, then the request rows. Dates keep their spacing from the anchor and shift to injection time.
- Enterprise tools read `ScenarioEvidenceBoard.CurrentCorrelation`. The live investigation binds `incident.CorrelationId` for the whole gather. A caller that did not bind a correlation gets the seed only.
- `POST /api/injector/stop` clears every plant fault and publishes `scenario.stopped` with no scenario id, so the board clears every correlation. `fault.cleared` removes that one correlation. `RemoveScenario` remains for a stop that names one scenario.
- The old `scripted` flag is `recordOnly`. A record-only fault command does not apply a second signal and does not write confounder or ground-truth rows. Replay recordings do not carry the old flag.
- One printout step serves every scenario. Timing `now` stamps the plant clock. A missing file fails the scenario start. SC-6 is the only scenario with a step, and it still confirms the same PDF: 2140 W, `rfSourcePower`, spec 1000–2000, instrument oscilloscope, one oscillation fault, and no `manualMeasurement` alert.

## Tests added

- `EachCorrelationServesItsScenarioRecordsAndClearRemovesThem`
- `TwoFaultRecordsStayOnTheirOwnCorrelations`
- `InjectedFaultsAndScenariosKeepRecordsOnTheirOwnCorrelations`
- `UnknownFaultEvidenceIsRejected`
- `ScSixPrintoutComesFromTheScenarioCatalog`
- `OscilloscopeScenarioAppliesThePrintoutOnce` now asserts the 2140 W sample
- `RecordOnlyFaultsDoNotStackConfoundersOrGroundTruth` (replaces the scripted-flag test)

`dotnet build -warnaserror` succeeded. `dotnet format MPS.sln --verify-no-changes` succeeded. `dotnet test MPS.sln`: 874 passed, 2 failed, 1 skipped.

The two failures also fail on a clean tree at `50ced27`, with the work stashed:

- `EffectiveLimitsGoldenTests.EveryConsumerReadsTheSameValuesAsTheGoldenSnapshot` — `sim.nominalTelemetry.2h.sha256` is `E4900D3B54663F9C1AB7ED4E0351610C99D0CD04DABE7A07ADF42D96430FF17D`, golden is `1CE889025570CD55EC46C3B29F2B9D55772B39316BD47384915E681B9CFB4D59`
- `EvalSuiteTests.TheReplayEvalPassesTheGatesAndTheCommittedReportIsCurrent` — indented JSON from the runner uses `\r\n` and `eval/results/latest.json` uses `\n`

The skipped test is `LiveModelEvalTests.TheLiveModelPicksTheExpectedClassAndNeverCitesARuledOutDecoy`.

## Outside this lane

- `tests/Application.Tests/Faults/FaultInjectionServiceTests.cs` — `Scripted` renamed to `RecordOnly` so the solution builds
- `src/Mps.Api/Http/ApiContracts.cs` and `src/Mps.Api/Http/FaultEvidenceBody.cs` — request fields for injector records
- `contracts/openapi.yaml` — optional `evidenceFaultId` and `evidence`
- `src/Mps.Application/Faults/FaultEvidenceAttachment.cs` — the records the injector and the board share

No edits under `web/`, `src/Mps.Agents/`, `src/Mps.Application/Spc/`, `StreamSpcPublisher.cs`, `SimulationTickService.cs`, `TelemetryPublisher.cs`, `spc-rules.yaml`, or `DemoResetService.cs`.

## Open

- A fault that ends because its duration elapsed does not publish `fault.cleared`, so its board rows stay until a scenario stop, a reset, or an explicit clear. Blocking: no.
- Confounder and ground-truth rows still live on `ConfounderBoard` by tool. Two free-form faults on the same tool can both see those rows. Correlation-keyed records do not mix. Blocking: no.
- `PlantSimulation.RunningScenarioId` is still one slot for the harness banner. Evidence is not. Blocking: no.
