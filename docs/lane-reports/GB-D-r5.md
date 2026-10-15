# Lane report — GB-D round 5

**Branch:** `lane/d-g6`
**Agent:** Grok Build
**Date:** 2026-10-14
**Scope:** G6 to-be swimlane, and the K6 deck-plan pointer. No application behavior. One glossary stop word so the badge name can appear in docs.

Copyright (c) 2026 Bryan C. Weis and Common Consulting Company.

---

## Done per issue

### G6 — to-be investigation swimlane

The brief did not give a GitHub issue number. The title block says G6, v1.0, 2026-10-14, and does not invent one.

| Deliverable | Path |
|---|---|
| Mermaid source and narrative | `docs/diagrams/to-be-flow.md` |
| ARCH D to-be PDF | `docs/diagrams/print/to-be-flow-archD.pdf` |
| ARCH D to-be SVG | `docs/diagrams/print/to-be-flow-archD.svg` |
| Diagram-only SVG | `docs/diagrams/print/to-be-flow.svg` |
| 150 dpi preview | `docs/diagrams/print/preview/to-be-flow-archD.png` |
| Raw mermaid SVG (not for the plotter) | `docs/diagrams/print/mermaid/to-be-flow.svg` |

Same stage bands as the as-is sheet: Detect, Triage, Gather evidence, Diagnose, Decide / approve, CAPA. Each band is marked illustrative / simulated. Durations are the expected-case split already used on the comparison sheet: 0.1 day, 0.2 day, 0.4 day, 0.5 day, 2.0 days, 40.3 days. Detect through Diagnose is 1.2 days. CAPA at 40.3 days is the record cycle.

Same six lanes, plus **MPS agent platform (StatusNeo)** on the bottom. Technician, process engineer, and supply chain stay on the sheet with a lane subtitle and no numbered card: "No hand-off", "Reads the rank", "Lot is joined".

The 13 numbers from the as-is MANUAL badges:

| Number | Badge | Lane | What it does now |
|---|---|---|---|
| 1 | AGENT | Platform | Statistical process control opens the ticket with tool, chamber, and recipe. |
| 2 | AGENT | Platform | The alert is the ticket. Nobody retypes it. |
| 3 | HUMAN | Quality engineer | Opens the ticket. The recipe step stays on it. |
| 4 | AGENT | Platform | Reads the manufacturing execution system. |
| 5 | AGENT | Platform | Reads the historian, the control chart, and the engineering-change log. |
| 6 | AGENT | Platform | Retrieves prior cases onto the ticket. |
| 7 | AGENT | Platform | Joins the supplier lot to the run. |
| 8 | AGENT | Platform | One evidence pack. No meeting. |
| 9 | AGENT | Platform | Ranks four cause classes and cites what would rule one out. |
| 10 | AGENT | Platform | The pack is the ask. Citations are attached. |
| 11 | HUMAN | Management | Approves on the pack with a signed token. The cause is cited. |
| 12 | AGENT | Platform | The action service writes the record from the approved pack. |
| 13 | AGENT | Platform | The token is the signature. No second signature. |

Each as-is error E1 through E11 has a green badge on the step that removes it, and a callout that says how. E1 is the spoken alarm: statistical process control opens the ticket with tool, chamber, and recipe. The human stays the approver. Agents do not write and do not approve.

Glossary on the sheet: MES, SPC, ECN, QMS, CAPA. Worked tool: fictional MX-400 etch.

Generator: `docs/diagrams/sheets.mjs` (`drawToBe`) and `sheet.mjs` (green `removed` tone, title block omits the issue line when no issue is set). Regenerate with `pwsh docs/diagrams/render.ps1 -- --only to-be-flow`. Title block: concept-1 logo, G6, v1.0, 2026-10-14, Prepared by StatusNeo, IP footer. Text is outlines. Minimum type is 14 pt.

`REMOVED` is now a glossary stop word in `tests/Simulator.Tests/GlossaryTests.cs`, next to MANUAL, ERROR, and RISK from the G5 badge fix. The badge name can appear in docs. It is not an acronym. That file is outside the design folders.

No `DESIGN_QUESTIONS.md`. Nothing in the contracts blocked this.

### K6 (#75) — deck plan

PLAN section 7 now points K6 slide 2 at the G6 sheet (`docs/diagrams/print/to-be-flow-archD.pdf`). Slide 1 stays the as-is swimlane. Slide 3 stays the side-by-side. The G4 screen-order sheet remains the architecture flow. `docs/analysis/current-vs-future-state.md` lists the G6 sheet with the other plotter files. Section 8 was left unchanged.

---

## How verified

- `node` via `pwsh docs/diagrams/render.ps1 -- --only to-be-flow --check` exited 0. The checker rejects type under 14 pt, missing glyphs, node overlaps, text overlaps, arrows through nodes or labels, and interior line crossings.
- Full render exited 0. The script rejects a PDF whose MediaBox is not about 2592 by 1728 pt, a PDF that embeds a font, and a PNG that is not 5400 by 3600. This sheet passed. Text is outlines.
- I looked at the preview and at crops of the header and stage bands, the lane labels, the gather joins, the diagnose card, the decide and CAPA elbows, the error-removed callouts, the glossary, the legend, and the title block. Badges sit inside the cards. Stage bands align. Lane subtitles sit in the label column. No overlapping or clipped labels in those views.
- mermaid-cli (Edge) wrote `print/mermaid/to-be-flow.svg`. Auto-layout is not the plotter file.
- `dotnet build MPS.sln --configuration Release` — 0 warnings, 0 errors. TreatWarningsAsErrors is on.
- `dotnet test MPS.sln --configuration Release --no-build` — 411 passed, 0 failed. Domain 19, ModelGateway 13, Application 167, Agents 77, Architecture 10, Orchestration 15, Connectors 7, MockEnterprise 4, Simulator 47, Contract 39, Api 13. Api.IntegrationTests matched no `Category!=PendingLane` tests and the run exited 0.
- The first full test failed one glossary check (`REMOVED` read as an acronym). The stop word above fixes it. The glossary test and the full suite were re-run after that change.
- `dotnet format MPS.sln --verify-no-changes` passed after the stop-word edit.
- No git network, no Azure CLI, no Docker, no git-credential-manager.

---

## Open questions for Bob

1. **Issue number.** The round-5 brief names G6 and does not give a GitHub issue. The title block has no issue id. Say if it should carry one.
2. **Which numbers stay human.** HUMAN 3 is the quality engineer opening the ticket. HUMAN 11 is management approving. AGENT 10 is the pack becoming the ask. AGENT 13 is the token standing as the signature, so CAPA has no second human sign-off. That is 11 agent steps and 2 human steps, matching the comparison sheet. Say if 10 or 13 should stay on a person.
3. **Stage split.** The 1.2-day and 40.3-day totals are the PLAN section 6.5 expected case. The split inside them is the illustrative split already on the comparison sheet. Say if the deck should use a different split.
4. **Empty lanes.** Technician, process engineer, and supply chain have a subtitle and no card. Say if those lanes should carry an unnumbered card instead.
5. **K6 slide 2.** It now uses this G6 swimlane. The G4 sheet is still in the repo as the screen-order flow. Say if the deck should show both.
6. **Stop word.** `REMOVED` was added in `tests/Simulator.Tests/GlossaryTests.cs` so docs can name the badge. That file is outside this lane's folders. The change is one token, same pattern as the G5 badge stop words.
