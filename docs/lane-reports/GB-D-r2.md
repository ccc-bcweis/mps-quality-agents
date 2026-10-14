# Lane report — GB-D round 3b

**Branch:** `lane/d-g4b`
**Agent:** Grok Build
**Date:** 2026-10-14
**Scope:** G4 (#56) investigation flow, SPEC 2.10 explainability. No application code. No contract changes.

The report path is `docs/lane-reports/GB-D-r2.md` as this pass required. The work is round 3b on top of the merged G4 swimlane.

Copyright (c) 2026 Bryan C. Weis and Common Consulting Company.

---

## Done per issue

### G4 (#56) — explainability on the investigation flow

Commit: `e138eed` `feat(G4): show explainability on the investigation flow (#56)`

| Deliverable | Path |
|---|---|
| Mermaid source and narrative | `docs/diagrams/investigation-flow.md` |
| ARCH D plotter PDF, v1.1 | `docs/diagrams/print/investigation-flow-archD.pdf` |
| ARCH D outlined SVG | `docs/diagrams/print/investigation-flow-archD.svg` |
| Diagram-only SVG | `docs/diagrams/print/investigation-flow.svg` |
| 150 dpi preview | `docs/diagrams/print/preview/investigation-flow-archD.png` |
| Raw mermaid-cli SVG (not for the plotter) | `docs/diagrams/print/mermaid/investigation-flow.svg` |

The swimlane still runs Alerts / Tickets → Investigation → Approval → CAPA. v1.1 adds four plain-language pieces and spells the acronyms that remain:

- **Tool schematic** (console), the evidence entry point. The engineer opens the ticket, the schematic is next, then triage. The card names chamber, gas, vacuum, chuck, and the radio-frequency source, and says a sensor opens its chart.
- **Why it matters** (Investigation Agent, AGENT badge). One plain line per evidence item, written on the pack after the enterprise reads and before the evidence gate.
- **Evidence matrix** (Resolution Agent, produced at Rank causes). Four classes, the signal that supports each one, and the signal that rules it out. An arrow drops from Rank causes, clear of the model-call card, onto that panel.
- **Glossary plain language** on the console lane. A glossary box expands SPC, CAPA, MES, QMS, PLM, ERP, and CMMS. The approval card says "Signed token" and the CAPA card says "Corrective and preventive action".

Generator: `docs/diagrams/sheets.mjs` (`drawInvestigation`) and `docs/diagrams/sheet.mjs` (optional lane subtitle, a text-free frame, per-sheet version). Other sheets stay on v1.0 until they are regenerated. Regenerate with `pwsh docs/diagrams/render.ps1 -- --only investigation-flow`.

Design questions, not contract edits: `docs/design-questions/GB-D-r2.md`.

---

## How verified

- `node docs/diagrams/render.mjs --only investigation-flow --check` exited 0. The checker rejects type under 14 pt, missing glyphs, node overlaps, text overlaps, arrows through nodes or labels, and interior line crossings.
- Full render, including mermaid-cli via Edge, exited 0. The renderer accepts the PDF only when the MediaBox is about 2592 × 1728 pt and the PDF embeds 0 fonts. Preview PNG is 5400 × 3600.
- I looked at the full preview and at crops of the schematic entry, the why-line and escalation dashes, the rank-to-matrix arrow beside the model-call card, the matrix columns, the glossary box, approval and CAPA, and the title block (logo, G4, v1.1, 2026-10-14, StatusNeo, copyright). The first arrow route rode the bottom of Rank causes; the shipped route leaves the bottom-left corner, stays in the gutter, and lands on the rule-out column.
- `dotnet build` 0 warnings, 0 errors. `dotnet test` exited 0: Domain 19, Architecture 10, Application 158, Simulator 36, MockEnterprise 4, Connectors 7, Contract 39, Api 11. Api.IntegrationTests matched no tests under the existing `Category!=PendingLane` filter, so that assembly did not execute any tests. `dotnet format --verify-no-changes` passed.
- No git network, no Azure CLI, no Docker, no git-credential-manager.

Not verified: a headed click-through. This pass did not change the wireframes.

---

## Open questions for Bob

1. **`whyItMatters` is not in contracts-v1.** The sheet shows where the line is written and checked. Putting the field on the wire is a contract change. Recommendation and options are in `docs/design-questions/GB-D-r2.md` question 1. Not blocking for this diagram. Blocking for agent and UI work that must persist the field.
2. **"Out of spec" and "Signals that rule it out".** The sheet uses the short SPEC 2.10 class name, and the brief's "rule out" header for what SPEC §2.4 calls the confounder. Question 2 in the same file. Not blocking.
3. **Schematic card is five lines.** Heater and wafer handler are named in the narrative and not on the card. Say if the card should drop "radio-frequency source" to make room, or if the narrative is enough.
