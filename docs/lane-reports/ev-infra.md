# Lane report: ev-infra

Branch `lane/ev-infra`, off `50ced27`. Issue #383 (folds in #369). Local commits only. Nothing was pushed. No Azure CLI.

Copyright (c) 2026 Bryan C. Weis and Common Consulting Company. MIT.

## Done

The investigation list no longer shows a placeholder and the saved record for the same incident and round. `AddAsync` is followed immediately by `InvestigationsInFlight.MarkSaved`, before the two notifier calls and the metrics write. `ListAsync`, `ListRoundsAsync`, and `GetLatestByIncidentAsync` also drop a placeholder whose id is already finished, or whose incident and round are already in the saved set. A later round that is still gathering stays beside the saved earlier round. Public investigation DTOs are unchanged.

The API container is 0.5 vCPU and 1 GiB. `cpu` and `memory` are parameters on `infra/modules/container-apps.bicep` and `infra/main.bicep`, set from `infra/main.bicepparam` (`0.5` and `1Gi`). `minReplicas` and `maxReplicas` stay 1. `.github/workflows/deploy.yml` does not mention cpu or memory, so it was left as it is. The running app keeps the old size until the next manual deploy.

Docs that stated the replica size or the monthly Azure cost now say 0.5 vCPU and 1 GiB, about $33 more per month, approved after the 18 Oct 2026 restart (working set 590-673 MiB against the 0.5 GiB limit, failed probes). The `mpsdemo-monthly` alert stays $40. October spend stays $4.13 month to date as of 2026-10-18 about 05:00 Eastern Time. ROI CSV numbers are unchanged.

| Commit | What |
|---|---|
| `1c5b1f8` | Drop the placeholder when the save returns, and filter the list. |
| `886f2a0` | Parameterize container cpu and memory, and lock the shape in the architecture test. |
| `057c457` | Operations, budget, and cost docs for the larger replica. |

## Files

Owned:

- `src/Mps.Application/Lifecycle/InvestigationService.cs`
- `src/Mps.Application/Lifecycle/InvestigationsInFlight.cs`
- `tests/Application.Tests/Lifecycle/InvestigationListRaceTests.cs`
- `infra/main.bicep`
- `infra/main.bicepparam`
- `infra/modules/container-apps.bicep`
- `infra/BUDGET.md`
- `docs/operations/runbook.md`
- `docs/roi-assumptions.md`

Outside the lane, because they already stated the current replica size or they lock the Bicep text:

- `tests/Architecture.Tests/DeployShapeTests.cs`
- `README.md`
- `docs/PLAN.md`
- `docs/SPEC.md`
- `docs/deck/roi-slides.md`
- `docs/deck/pov-content.md`
- `docs/demo/DEMO-SCRIPT.md`
- `docs/diagrams/README.md`
- `docs/diagrams/cicd.md`
- `docs/diagrams/physical.md`
- `docs/diagrams/to-be-flow.md`

`web/` was not edited. Agent evidence, prompts, and hypothesis contracts were not edited.

## Design

The placeholder id and the saved id differ. They share the incident and the round. Ending the placeholder only in `finally` left both rows visible while `InvestigationChangedAsync`, `IncidentChangedAsync`, and the metrics write were in flight. `MarkSaved` calls `End`, which records `SavedAs` before it removes the placeholder, so a get by the placeholder id still finds the saved row if a notifier throws afterwards.

The list filter matches id, a recorded `SavedAs`, or the same incident and the same round. Matching the incident alone would hide round 2 while round 1 is saved. `ADR-010` and `tests/Api.Tests/ResetSpeedTests.cs` still describe the 0.25 vCPU app as the 17 Oct measurement. Those sentences are the history of that reset, so they were left.

## Tests added

`tests/Application.Tests/Lifecycle/InvestigationListRaceTests.cs`:

- `TheListHasOneEntryWhileTheNotifierIsHeldAndOneAfter`
- `ListDropsAPlaceholderWhenThatRoundIsAlreadySaved`
- `ALaterRoundStillGatheringStaysBesideTheSavedRound`

`DeployShapeTests` now requires `cpu: json(cpu)`, `memory: memory`, the `0.5` / `1Gi` defaults in `main.bicep` and `main.bicepparam`, and that `deploy.yml` contains neither `cpu=` nor `memory=`.

## How it was verified

`dotnet build MPS.sln --configuration Release -warnaserror` succeeded: 0 warnings, 0 errors.

`dotnet format MPS.sln --verify-no-changes --no-restore` exited 0.

`dotnet test MPS.sln --configuration Release --no-build` exited 1. Totals: passed 873, failed 2, skipped 1, total 876.

| Assembly | Passed | Failed | Skipped |
|---|---:|---:|---:|
| Domain.Tests | 39 | 0 | 0 |
| ModelGateway.Tests | 18 | 0 | 0 |
| Agents.Tests | 150 | 1 | 0 |
| Bus.Tests | 6 | 0 | 0 |
| Application.Tests | 266 | 0 | 0 |
| Architecture.Tests | 16 | 0 | 0 |
| Orchestration.Tests | 25 | 0 | 0 |
| Connectors.Tests | 13 | 0 | 0 |
| MockEnterprise.Tests | 4 | 0 | 0 |
| Simulator.Tests | 94 | 0 | 0 |
| Contract.Tests | 41 | 0 | 0 |
| Api.IntegrationTests | 23 | 0 | 0 |
| Api.Tests | 178 | 1 | 1 |

Application.Tests (266, including the three new facts) and Architecture.Tests (16) passed. The skipped test is `LiveModelEvalTests.TheLiveModelPicksTheExpectedClassAndNeverCitesARuledOutDecoy`.

The two failures are outside this diff. Their tests, `eval/results/latest.json`, the golden file, and the simulator sources are unchanged from `50ced27`.

- `EvalSuiteTests.TheReplayEvalPassesTheGatesAndTheCommittedReportIsCurrent` differs at index 1. The committed report is LF. `JsonSerializer` on this Windows runtime emits CRLF. The replay gates themselves are not the mismatch.
- `EffectiveLimitsGoldenTests.EveryConsumerReadsTheSameValuesAsTheGoldenSnapshot` differs only on `sim.nominalTelemetry.2h.sha256`: golden `1CE889025570CD55EC46C3B29F2B9D55772B39316BD47384915E681B9CFB4D59`, this machine `E4900D3B54663F9C1AB7ED4E0351610C99D0CD04DABE7A07ADF42D96430FF17D`. The golden file was not rewritten.

Standalone Bicep CLI `0.47.16` (`bicep build infra/main.bicep` and `bicep build-params`) compiled. The template default is cpu `0.5` and memory `1Gi`. The container resources are `[json(parameters('cpu'))]` and `[parameters('memory')]`. Replicas are 1 and 1. The parameters file carries the same values. `az` was not used. CI still runs its own Bicep check (CLI v0.48.1).

`web/` was not touched, so the npm checks were not run.

## Open

- The whole-solution test run is not green on this Windows checkout, for the two failures above. Linux CI is the place those two were written to pass. This lane did not change them.
- The $40 budget alert was left in place. A full month at 0.5 vCPU / 1 GiB is about $33 more than the old pair, so the 80% actual alert can fire. Raising `monthlyBudgetUsd` was not part of this lane.
- These commits change the template. They do not resize the replica that is already running. That happens on the next manual `workflow_dispatch`.
- Parallel lanes may also edit `README.md`, `docs/SPEC.md`, `docs/PLAN.md`, and the deck files listed above.
