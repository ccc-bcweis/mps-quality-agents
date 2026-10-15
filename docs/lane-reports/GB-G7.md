# Lane report — G7 (fab floor and company overview)

**Branch:** `lane/g7`  
**Agent:** Grok Build  
**Date:** 2026-10-14  
**Scope:** G7 physical fab floor, K7 opening slide in the deck outline. No application code. No change outside `docs/diagrams/`, `docs/deck/`, `docs/PLAN.md` section 7, and these reports.

The same report is in `docs/lane-reports/G7.md`.

Copyright (c) 2026 Bryan C. Weis and Common Consulting Company.

---

## Done per issue

### G7 — physical fab floor

Commit `c3accf5` `feat(G7): add the physical fab floor sheet and label overlap check`

ARCH D sheet (36 by 24 in), plan view, separate from the console tool schematic and from the Azure sheet in `docs/diagrams/physical.md`.

- `docs/diagrams/fab-floor.mjs` draws the floor. `docs/diagrams/fab-floor.md` describes it. Registered in `docs/diagrams/sheets.mjs` as slug `fab-floor`.
- Cleanroom bay 1: D-01 deposition, E-01 etch, S-01 strip, C-01 clean. Cleanroom bay 2: D-02, E-02, S-02, C-02. Same eight tools as the demo fleet. Load ports face the rail.
- Overhead wafer rail: one loop between the bays, a stocker at each end.
- North, south, east, and west utility chases. The sub-fab callout (pumps, chillers, abatement) sits in the north chase. That level is not drawn.
- Right-hand plans of each tool type, with chambers, load ports, wafer handler, and gas box or chemical rack. Etch shows the edge ring. Clean is one chamber.
- Print files: `docs/diagrams/print/fab-floor-archD.svg`, `docs/diagrams/print/fab-floor-archD.pdf`, `docs/diagrams/print/fab-floor.svg`, and the 150 dpi preview `docs/diagrams/print/preview/fab-floor-archD.png`.
- Label check in `docs/diagrams/sheet.mjs` (`collectLabelIssues`). Boxes are inflated by 1.5 pt. A label fails if it overlaps another label, escapes its clip, or covers drawing ink. The fab floor requires a clip on every figure label (`strictLabels`).

The floor is the teaching plan at Plant A. It is labeled Illustrative. It is not a survey of a real building.

### K7 — company overview, opening slide

Commit `cd55912` `feat(K7): add the company overview map and opening slide outline`

Placed in `docs/PLAN.md` section 7, ahead of numbered slide 1. Slides 1–10 are not renumbered. Section 6.5 still points at deck slide 5.

- Meridian Precision Systems is described as a fictional company that builds chip-process tools and runs three plants.
- Map: `docs/diagrams/mps-sites.svg`. Invented coast, three pins, callout cards, north arrow. Caption: fictional coast, not a real city, not to scale. Marked Illustrative.
- Picture frames, both marked Illustrative, with generation prompts on the K7 bullet: `docs/deck/exterior-illustrative.svg` and `docs/deck/cleanroom-illustrative.svg`. No photograph was generated.
- `docs/diagrams/render.mjs` writes the map and the frames, and runs a small proof that the overlap check, the clip check, and the drawing-ink check each fire, and that a clear pair does not.

### Plant names (proposal for Lane S1)

Coined in `docs/diagrams/plants.mjs` and repeated on the floor, the map, and the K7 table.

| Plant | Name | Role |
|---|---|---|
| A | Cindervane | Front-end fab. Deposition and etch. This sheet is that floor. |
| B | Lowspindle | Second fab. A similar tool mix. |
| C | Rookspur | Photoresist strip, wafer clean, and a pilot line. |

These names are not locked. Searched 2026-10-14. Exact strings Cindervane, Lowspindle, and Rookspur did not come up as companies or places. An earlier candidate, Nimwell, was dropped: Nimwell P is a real medicine brand. Nearby words that were not used: Cinderveil (a novel), Crookspur (a game location).

---

## How verified

- No git network, no Azure CLI, no Docker. Local commits only. Sample git hooks only; nothing pushed.
- `pwsh -File docs/diagrams/render.ps1 -- --check` exited 0 after the Rookspur rename. It validated the site map, both picture frames, the six existing sheets, and `fab-floor`. Existing sheet files were byte-identical afterward (`git status` showed no edits under the other print slugs).
- `pwsh -File docs/diagrams/render.ps1 -- --only fab-floor --skip-mermaid` exited 0 and wrote `fab-floor-archD.pdf`. The generator checks the PDF page box (36 by 24 in) before it prints the path.
- Floor preview read by eye: two bays, four chases, rail loop with two stockers, eight tools with ports toward the rail, four type plans, legend, title block. South chase names Lowspindle and Rookspur. No label collision visible.
- Map and both frames screenshotted with headless Edge. The coast is a closed invented shape with a side inlet. Pins A, B, and C lead to cards for Cindervane, Lowspindle, and Rookspur. The gold rule on each frame sits clear of the word Illustrative.
- `git diff` of `docs/PLAN.md` is section 7 only.

Not verified: a large-format print of the PDF, and a headed browser pass. The pictures are static SVG and PDF.

---

## Open questions for Bob

1. **Plant names.** Confirm Cindervane, Lowspindle, and Rookspur with Lane S1, or send replacements. They are a proposal, repeated in three places so a rename is a one-file change in `docs/diagrams/plants.mjs` plus a regenerate and the K7 table.
2. **Issue numbers.** The sheet title block says Issue G7. `docs/PLAN.md` section 8 was left unchanged. Add a row if the board has numbers.
3. **One floor.** Plants B and C are named, not drawn as their own buildings. Say if the pitch needs a second sheet.
4. **Pictures.** The exterior and cleanroom frames are empty on purpose, with prompts on the K7 slide. A later image pass should keep the word Illustrative and should not depict a real plant.
5. **Preview PNG.** `print/preview/fab-floor-archD.png` is committed, same as the other sheets. Re-render if you want a fresh 150 dpi file.
