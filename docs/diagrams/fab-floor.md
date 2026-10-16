# Fab floor plans

**Issue:** G7
**Copyright (c) 2026 Bryan C. Weis and Common Consulting Company**

One plan-view floor per demo site. The bays, bay names, and tool ids come from `data/fleet/sites.yaml`, the same file the console reads. The Production line bay headers and these sheets therefore always agree. These are building drawings. They are not the console tool schematic, and they are not the Azure physical architecture in [`physical.md`](physical.md).

The pictures are illustrative. They are not a survey, and they are not real buildings. The place names are invented.

| Sheet | Site | Bays |
|---|---|---|
| G7a | Northwater Fab, Cedar Reach, Valecrest | Cleanroom bay 1 (D-01, E-01, S-01, C-01), Cleanroom bay 2 (D-02, E-02, S-02, C-02), Cleanroom bay 3 (D-03, D-04), Cleanroom bay 4 (E-03, E-04) |
| G7b | Ironwood Fab, Ironwood Bench, Eastmere | Cleanroom bay 1 (I-D-01, I-D-02), Cleanroom bay 2 (I-E-01 to I-E-03), Cleanroom bay 3 (I-D-03, I-D-04), Cleanroom bay 4 (I-E-04, I-S-01, I-C-01) |
| G7c | Lumen Pilot Works, Lumen Yard, Westfold | Cleanroom bay 1 (P-S-01 to P-S-03), Cleanroom bay 2 (P-C-01 to P-C-03), Pilot line bay 3 (P-D-01, P-D-02), Pilot line bay 4 (P-E-01, P-E-02) |

Each bay label also names the families in it, for example "Deposition, etch, strip, clean". The console shows the same caption. Load ports face the overhead rail, and a stocker sits at each end of the rail. The south chase lists the site's wafer flows from `sites.yaml`. The right side of each sheet is a larger plan of each tool type: MPS-D7, MPS-E7, MPS-S7, and MPS-C7.

The earlier single sheet, `fab-floor-archD`, drew a generic Plant A. It has been retired.

## Files

These are ARCH D sheets (36 by 24 in, landscape). There is no mermaid source, because a floor plan does not survive automatic layout.

- `print/fab-floor-<site>-archD.svg` and `.pdf`, where `<site>` is `northwater`, `ironwood`, or `lumen`
- `print/preview/fab-floor-<site>-archD.png` (150 dpi)
- `print/slides/fab-floor-<site>-16x9.png` (3840 by 2160, for slides)

To regenerate, run `node docs/diagrams/render.mjs --only fab-floor --skip-mermaid`. On Linux, set `MPS_DIAGRAM_TOOLS` and `MPS_BROWSER`. The generator checks that label boxes do not overlap, do not escape their clip, and do not cover drawing ink.
