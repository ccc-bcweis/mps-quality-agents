# Lane report — GB-D round 4

**Branch:** `lane/d-g5`
**Agent:** Grok Build
**Date:** 2026-10-14
**Scope:** G5 (#74) current-state investigation, the as-is versus to-be sheet, and K6 (#75) in the deck plan. No application code. No contract changes.

Copyright (c) 2026 Bryan C. Weis and Common Consulting Company.

---

## Done per issue

### G5 (#74) — as-is investigation, comparison sheet, and the consultative note

Commits:

- `76abd5a` `feat(G5): add the as-is investigation swimlane (#74)`
- `38ab305` `feat(G5): add the as-is versus to-be comparison sheet (#74)`
- `8c6322b` `docs(G5): add the current versus future state note (#74)`

| Deliverable | Path |
|---|---|
| Mermaid source and narrative | `docs/diagrams/as-is-flow.md` |
| ARCH D as-is PDF | `docs/diagrams/print/as-is-flow-archD.pdf` |
| ARCH D as-is SVG | `docs/diagrams/print/as-is-flow-archD.svg` |
| Diagram-only SVG | `docs/diagrams/print/as-is-flow.svg` |
| 150 dpi as-is preview | `docs/diagrams/print/preview/as-is-flow-archD.png` |
| Raw mermaid SVG (not for the plotter) | `docs/diagrams/print/mermaid/as-is-flow.svg` |
| ARCH D comparison PDF | `docs/diagrams/print/as-is-vs-to-be-archD.pdf` |
| ARCH D comparison SVG | `docs/diagrams/print/as-is-vs-to-be-archD.svg` |
| Diagram-only comparison SVG | `docs/diagrams/print/as-is-vs-to-be.svg` |
| 150 dpi comparison preview | `docs/diagrams/print/preview/as-is-vs-to-be-archD.png` |
| Consultative note | `docs/analysis/current-vs-future-state.md` |

The as-is sheet is a StatusNeo reading of Meridian's current investigation. Six lanes: Operator, Technician, Process engineer, Quality engineer, Supply chain, Management. Stage bands across the top, each marked illustrative / simulated: Detect 0.5 day, Triage 0.5 day, Gather evidence 2.0 days, Diagnose 1.0 day, Decide / approve 2.0 days, CAPA 62 days. Detect through Diagnose is the simulated 4.0-day path to a hypothesis. CAPA is the simulated 62-day record cycle.

Thirteen numbered red MANUAL badges, one on every hand-off, with red arrows. Eleven amber ERROR RISK badges, with a callout box: transcription, data gaps, a missed engineering change, a wrong lot, bias toward the last known cause, an uncited approval, and the re-keyed record. Glossary on the sheet for MES, SPC, ECN, QMS, and CAPA. The worked alarm is on the fictional MX-400 etch.

The comparison sheet puts a condensed as-is on the left and a condensed G4 path on the right. The stage bands match. The benefits column is time to root cause (4.0 days to 1.2 days), fewer hand-offs (13 manual to 2 human), a traceable evidence trail, a consistent four-cause check, and the human staying the approver. Every figure on that sheet is marked simulated. Decide / approve stays 2.0 days on both sides. The to-be CAPA cycle is 40.3 days.

Generator hook: `docs/diagrams/sheets.mjs` (`drawAsIs`, `drawCompare`) and `sheet.mjs` (a row of MANUAL and ERROR RISK badges). Regenerate with `pwsh docs/diagrams/render.ps1 -- --only as-is-flow` and `--only as-is-vs-to-be`. Title block: concept-1 logo, G5, issue #74, v1.0, 2026-10-14, Prepared by StatusNeo, IP footer. Text is outlines. Minimum type is 14 pt.

No `DESIGN_QUESTIONS.md`. Nothing in the contracts blocked this.

### K6 (#75) — three deck slides

Commit:

- `a98fd76` `docs(G5): add K6 slides to the deck plan (#75)`

PLAN section 7 now has K6: as-is swimlane, to-be investigation flow (the existing G4 sheet), and the side-by-side with benefits. Section 8 was left unchanged. The brief asked for the addition in section 7.

---

## How verified

- `node docs/diagrams/render.mjs --only as-is-flow --check` and `--only as-is-vs-to-be --check` exited 0. The checker rejects type under 14 pt, missing glyphs, node overlaps, text overlaps, arrows through nodes or labels, and interior line crossings.
- Full render of each sheet exited 0. The script rejects a PDF whose MediaBox is not about 2592 by 1728 pt, a PDF that embeds a font, and a PNG that is not 5400 by 3600. Both sheets passed those checks. Text is outlines.
- I looked at the as-is preview and at crops of the stage bands, the gather joins (meeting arrows into the top and bottom of the card), the approval elbows, the error-risk callouts, the glossary, and the title block.
- I looked at the comparison preview and at crops of the header, the gather row, the decide and CAPA rows, the benefits column, the glossary, and the title block. Badges sit inside the cards. Stage bands align. No overlapping or clipped labels in those views.
- mermaid-cli (Edge) wrote `print/mermaid/as-is-flow.svg`. Auto-layout is not the plotter file. The comparison sheet has no mermaid source.
- `dotnet build` 0 warnings, 0 errors. `dotnet test` 369 passed, 0 failed. `dotnet format --verify-no-changes` passed. The integration assembly reported no tests matching `Category!=PendingLane` and did not fail the run. That filter was already on the solution.
- No git network, no Azure CLI, no Docker, no git-credential-manager.

---

## Open questions for Bob

1. **Stage split.** The 4.0-day and 1.2-day totals are the PLAN section 6.5 baseline and the expected 70 percent reduction. The split inside them (0.5 / 0.5 / 2.0 / 1.0 and 0.1 / 0.2 / 0.4 / 0.5) is illustrative, so the bands can be compared. Say if the deck should use a different split.
2. **5 to 15 days versus 4.0 days.** SPEC section 1 still says 5 to 15 days to a hypothesis and names four roles. This note uses six roles and the 4.0-day worked baseline so it matches the ROI model. Confirm the deck should say 4.0 days.
3. **Approval days.** Decide / approve is 2.0 days on both sides because the ROI model does not shrink the 5.5 days outside the hypothesis. Confirm the deck should not show a faster approval.
4. **Hand-off quote.** The sufficient path is 13 manual hand-offs against 2 human hand-offs (open, then approve). The escalation path adds a human owner for the gap and is not inside the 2. Confirm "13 to 2" is the sentence for the slide.
5. **To-be slide art.** K6 slide 2 points at the existing G4 sheet (`investigation-flow-archD.pdf`, v1.1). The comparison sheet is the condensed version with matching stage names. Say if slide 2 should be a new drawing instead.
6. **Section 8.** K6 is in the deck plan only. PLAN section 8 has no G5 or K6 row, and the issue totals were not changed.
