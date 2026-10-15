# Diagrams

Copyright (c) 2026 Bryan C. Weis and Common Consulting Company.

Pitch drawings for Meridian Precision Systems. The mermaid sources are the review text. The plotter files are separate, hand-composed sheets.

| Issue | Source | What it shows |
|---|---|---|
| G1 (#46) | [`logical.md`](logical.md) | Who may decide, and who may only read |
| G2 (#47) | [`physical.md`](physical.md) | Azure demo resources and the numbers on the box |
| G3 (#48) | [`cicd.md`](cicd.md) | Lanes, CI, review, and the manual deploy |
| G4 (#56) | [`investigation-flow.md`](investigation-flow.md) | Swimlane from Alerts to CAPA. Agent steps, the tool schematic, why-lines, and the evidence matrix |
| G5 (#74) | [`as-is-flow.md`](as-is-flow.md) | Current investigation. Manual hand-offs, error risks, simulated stage durations |
| G6 | [`to-be-flow.md`](to-be-flow.md) | To-be investigation. Same stages and lanes, plus the MPS agent platform. Agent steps replace the manual hand-offs |
| G5 (#74) | `print/as-is-vs-to-be-archD.pdf` | As-is and to-be side by side, matching stage bands, simulated benefits |
| G7 | [`fab-floor.md`](fab-floor.md) | Physical fab floor. Cleanroom bays, overhead rail, utility chases. Not the Azure sheet. |
| K7 | [`mps-sites.svg`](mps-sites.svg) | Opening-slide map. Three fictional plants on an invented coast. |

## What to print

Send these to the large-format printer. Page size is ARCH D landscape: 36 in wide by 24 in tall. Margins are zero.

- `print/logical-archD.pdf`
- `print/physical-archD.pdf`
- `print/cicd-archD.pdf`
- `print/investigation-flow-archD.pdf`
- `print/as-is-flow-archD.pdf`
- `print/to-be-flow-archD.pdf`
- `print/as-is-vs-to-be-archD.pdf`
- `print/fab-floor-archD.pdf`

Each sheet also has an SVG twin (`print/<name>-archD.svg`) and a diagram-only SVG (`print/<name>.svg`). Preview PNGs at 150 dpi (5400 × 3600) are in `print/preview/`.

The title block carries the concept-1 mark, the diagram title, version v1.0, the date 2026-10-14, “Prepared by StatusNeo”, and “(c) 2026 Bryan C. Weis and Common Consulting Company”. Type is at least 14 pt. Text is outlines, so the PDF does not embed a font.

`print/mermaid/<name>.svg` is the raw mermaid-cli rendering. It is there so the source can be traced. It is not the plotter file. Auto-layout crosses these graphs.

## Regenerate

From the repo root, with Node and Microsoft Edge installed:

```powershell
pwsh docs/diagrams/render.ps1
```

The script installs `opentype.js` and `puppeteer-core` under `%TEMP%\mps-diagram-tools` (not in this repo) and runs `mmdc` with Edge as the browser. npm may download `@mermaid-js/mermaid-cli`. Docker and the Azure CLI are not used.

```powershell
pwsh docs/diagrams/render.ps1 -- --only logical
pwsh docs/diagrams/render.ps1 -- --skip-mermaid
pwsh docs/diagrams/render.ps1 -- --check
```

`--check` writes the SVGs and runs the overlap checks. It skips PDF, PNG, and mermaid-cli.

IBM Plex Sans (SIL Open Font License, reserved name “Plex”) lives in `fonts/` with `OFL.txt`. The sheets outline those files. Do not substitute Segoe UI on a plotter sheet.
