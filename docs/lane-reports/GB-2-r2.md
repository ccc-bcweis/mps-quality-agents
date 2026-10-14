# Lane report: GB-2 round 2

Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

Branch `lane/2b-why`. Local commits only. Nothing was pushed. Contract files, `Mps.Domain`, `Mps.Api`, `Mps.Application`, and `data/` were not changed.

| Issue | Commit | What landed |
|---|---|---|
| UX3 (#61) | `97cabc2` | `whyItMatters` on every cited evidence id, `signalId` on sensor signals, and `Investigation` evidence matrix for all four root-cause classes. Prompts are 1.1.0. Grounding repairs a gap and rejects an id that is not in the pack. |

## How it was verified

On this worktree, after the code was final:

- `dotnet build MPS.sln --configuration Release` succeeded with 0 warnings and 0 errors. Warnings are errors in `Directory.Build.props`.
- `dotnet test MPS.sln --configuration Release` exited 0.
- `dotnet format MPS.sln --verify-no-changes` exited 0.

| Project | Passed |
|---|---|
| Domain.Tests | 19 |
| ModelGateway.Tests | 13 |
| Agents.Tests | 77 |
| Orchestration.Tests | 15 |
| Application.Tests | 158 |
| Architecture.Tests | 10 |
| Contract.Tests | 39 |
| Api.Tests | 11 |
| Simulator.Tests | 36 |
| Connectors.Tests | 7 |
| MockEnterprise.Tests | 4 |

`Api.IntegrationTests` reported no match for `Category!=PendingLane`. That filter is the project's `VSTestTestCaseFilter`. The solution run stayed green. No live model key was used. Docker and Azure CLI were not run.

UX3 checks:

- `PlainSentence` accepts one sentence of at most 240 characters and rejects a blank, a lowercase start, a missing terminator, two sentences, a sentence over 240 characters, a denylist token, and an all-capitals token.
- `WhyItMattersCatalog` uses a matching template, prefers the template with more constraints, skips an invalid template sentence, ignores a template with no constraints, reads a fleet `sensors` id, and still explains a record when the file is missing or the YAML is broken.
- `GroundingValidator` repairs a missing explanation and a missing matrix, repairs an invalid sentence, repairs a missing sensor `signalId`, and clears a `signalId` on a record that is not a sensor. It rejects `why-it-matters-unknown`, `why-it-matters-not-cited`, and `matrix-unknown-evidence-id`.
- `tests/Agents.Tests/Fixtures/s1-resolution.json` parses, grounds with `Repaired` false, and round-trips through `ReplayChatClient` under `PromptHasher`.
- `S1StoresWhyItMattersAndAFourClassMatrixAndReplaysThem` runs S1 through the gateway with recording on. Engineering change supports ECO-4471. Equipment calibration or alignment is neutral on WO-19. Supply chain is ruled out. Process drift is ruled out on a sensor id. Every stored explanation is a valid sentence, and every sensor explanation has a `signalId`. A stored replay turn contains `whyItMatters` and `evidenceMatrix`. A second run with a throwing reasoner is not called and still has the four-class matrix.
- Existing ranker outcomes are unchanged: S1 is still `engineeringChange` for ECO-4471 with WO-19 as counter-evidence. An unknown evidence id still escalates with `grounding:` and `unknown-evidence-id:`.

## Behavior Bob should expect

Prompt versions are `1.1.0` in `prompts/investigation.md` and `prompts/resolution.md`. The investigation prompt does not contain the phrase "resolution agent". `QualityMockReasoner` still ranks through `RootCauseRanker`. `ResolutionJson` always writes `whyItMatters` and `evidenceMatrix` on a recommendation.

`whyItMatters` is one plain sentence. The denylist includes spc, capa, pm, eco, and the other operations abbreviations in `PlainSentence`. A token of two or more letters that is all capitals is also rejected. The cause line may still name ECO-4471. That split is design question 4.

A sensor is telemetry or SPC. Its `signalId` comes from a template, then a fleet sensor, then the parameter in kebab case, then the literal `sensor`. Fleet files in this worktree have no `sensors` list yet, so the demo path uses kebab case (`esc_temp` becomes `esc-temp`) until GB-1 adds sensors.

The matrix has one row for each of `engineeringChange`, `equipmentCalibrationOrAlignment`, `supplyChainMaterial`, and `processDrift`. Stance is `supports`, `rulesOut`, or `neutral`. The chosen class supports its own evidence. Another class that has its own evidence is neutral. A class with no own evidence is ruled out and still cites an id from the pack. Process drift cites a signal when one exists. On S1 that is a rules-out row, because the signal shape is oscillation.

`GroundingValidator` rejects a structural failure and any explanation or matrix id that is not in the pack. An explanation id that is in the pack but not in `evidenceIds` is `why-it-matters-not-cited`. A missing sentence, an invalid sentence, a missing sensor id, or an incomplete matrix is repaired from `WhyItMattersCatalog`. The published claim is `GroundingResult.Claim`. `Repaired` is true when that claim was filled in.

`InvestigationSession.Explanations` and `InvestigationSession.EvidenceMatrix` hold the result. The draft matrix is written when the pack is stored. A gate escalation or a grounding rejection clears hypotheses and leaves that draft. An accepted claim overlays its explanations on the pack explanations and replaces the matrix. `InvestigationSnapshot` still omits both collections. The console cannot show them until the domain and API mapping in design question 1.

Templates are optional. The loader walks up from the current directory and from `AppContext.BaseDirectory` to `data/`. A missing or unreadable file keeps the built-in sentences. The file shape GB-1 can match is in `docs/design-questions/GB-2-r2.md`.

## Open questions for Bob

The four questions, with options and whether each one blocks, are in `docs/design-questions/GB-2-r2.md`.

1. Domain `EvidenceItem` / `InvestigationSnapshot` and the API mapper have no v1.1 `whyItMatters`, `signalId`, or `evidenceMatrix`. Blocking for the console. Not blocking this lane.
2. Template file shape for GB-1. Not blocking.
3. `signalId` falls back to kebab case until fleet `sensors` exist. Not blocking.
4. The acronym ban applies to `whyItMatters`. The cause may still say ECO-4471. Not blocking.
