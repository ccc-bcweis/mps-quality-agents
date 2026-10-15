# Physical fab floor

**Issue:** G7
**Copyright (c) 2026 Bryan C. Weis and Common Consulting Company**

Plan view of the demo floor at Plant A, Northwater Fab. It is a building drawing. It is not the console tool schematic, and it is not the Azure physical architecture in [`physical.md`](physical.md).

The picture is illustrative. It is not a survey, and it is not a real building.

## What the sheet shows

Two cleanroom bays. Each bay holds one tool from each family, matching the demo fleet:

| Bay | Tools, left to right |
|---|---|
| Cleanroom bay 1 (fleet bay 1) | D-01 deposition, E-01 etch, S-01 strip, C-01 clean |
| Cleanroom bay 2 (fleet bay 2) | D-02, E-02, S-02, C-02 |

Load ports face the overhead rail. Chambers sit on the far side of each tool. The etch tools draw an inner ring for the edge ring.

The rail is one loop between the bays. A stocker at each end holds wafer carriers. North, south, east, and west corridors are utility chases (gases, exhaust, and power). The sub-fab callout says pumps, chillers, and abatement are on the level below the north chase. That level is not drawn.

The right side of the sheet is a larger plan of each tool type: deposition (MPS-D7), etch (MPS-E7), strip (MPS-S7), and clean (MPS-C7). Each plan names the chambers, the load ports, the wafer handler, and the gas box or chemical rack.

Plant B, Ironwood Fab, runs a similar mix, and its etch runs hotter. Plant C, Lumen Pilot Works, runs photoresist strip, wafer clean, and a pilot line. Those plants are named on the sheet and drawn on [`mps-sites.svg`](mps-sites.svg). The names are the ones locked for the three fictional plants. Scenario SC-5, a supplier lot shared by Northwater and Ironwood, is not a room on this floor.

## Files

ARCH D sheet (36 by 24 in, landscape), v1.0, 2026-10-14. There is no mermaid source. A floor plan does not survive automatic layout.

- `print/fab-floor-archD.svg`
- `print/fab-floor-archD.pdf`
- `print/fab-floor.svg` (the plan without the title block)

Regenerate with `pwsh docs/diagrams/render.ps1`. The generator checks that label boxes do not overlap, do not escape their clip, and do not cover drawing ink.
