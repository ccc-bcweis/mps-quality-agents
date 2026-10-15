# Lane report: GB-D round 5

Branch `lane/d-g6`. Local commits only. Nothing was pushed.

Copyright (c) 2026 Bryan C. Weis and Common Consulting Company. MIT.

Detail is in `docs/lane-reports/GB-D-r5.md`.

## Done

| Issue | What |
|---|---|
| G6 | To-be swimlane in the same stages and lanes as the as-is sheet, plus MPS agent platform (StatusNeo). Violet AGENT badges keep the manual numbers they replace. Teal HUMAN badges are on the open (3) and the approval (11). Green badges and a callout map E1 through E11 to how each error is removed. Human stays the approver. |
| K6 (#75) | Deck plan slide 2 now uses `docs/diagrams/print/to-be-flow-archD.pdf`. Slide 1 stays as-is. Slide 3 stays the side-by-side. |

No contract files were changed. The brief did not give G6 a GitHub issue number, so the title block does not invent one.

## How it was verified

`pwsh docs/diagrams/render.ps1 -- --only to-be-flow` exited 0 (overlap check, outlined PDF at ARCH D, 5400×3600 preview). I looked at the preview and at crops of the stages, lanes, gather joins, decide and CAPA, callouts, glossary, legend, and title block.

`dotnet build MPS.sln --configuration Release` — 0 warnings, 0 errors. `dotnet test MPS.sln --configuration Release --no-build` — 411 passed, 0 failed. Api.IntegrationTests matched no `Category!=PendingLane` tests and the run exited 0. `dotnet format MPS.sln --verify-no-changes` passed.

No git network, no Azure CLI, no Docker.

## Open questions for Bob

- No issue number was in the brief. Say if the title block should carry one.
- HUMAN 3 opens the ticket. HUMAN 11 approves. AGENT 10 and AGENT 13 are platform steps, so the count stays 11 agent and 2 human. Say if 10 or 13 should stay on a person.
- Durations are the illustrative expected-case split (0.1 / 0.2 / 0.4 / 0.5 / 2.0 / 40.3). Say if the deck should use another split.
- Technician, process engineer, and supply chain have a subtitle and no card. Say if they should have an unnumbered card.
- `REMOVED` is a glossary stop word in `tests/Simulator.Tests/GlossaryTests.cs`, same pattern as the G5 badge words. That file is outside the design folders.
