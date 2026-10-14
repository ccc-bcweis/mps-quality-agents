# Lane report — GB-D round 3

**Branch:** `lane/d-g4`  
**Agent:** Grok Build  
**Date:** 2026-10-14  
**Scope:** G4 (#56) end-to-end investigation flow. No application code. No contract changes.

Copyright (c) 2026 Bryan C. Weis and Common Consulting Company.

---

## Done per issue

### G4 (#56) — investigation swimlane and the Investigation screen timeline

Commits:

- `b109a54` `feat(G4): add investigation flow swimlane (#56)`
- `2706906` `feat(G4): show the agent-step timeline on Investigation (#56)`

| Deliverable | Path |
|---|---|
| Mermaid source and narrative | `docs/diagrams/investigation-flow.md` |
| ARCH D plotter PDF | `docs/diagrams/print/investigation-flow-archD.pdf` |
| ARCH D outlined SVG | `docs/diagrams/print/investigation-flow-archD.svg` |
| Diagram-only SVG | `docs/diagrams/print/investigation-flow.svg` |
| 150 dpi preview | `docs/diagrams/print/preview/investigation-flow-archD.png` |
| Raw mermaid-cli SVG (not for the plotter) | `docs/diagrams/print/mermaid/investigation-flow.svg` |
| Investigation wireframe | `design/wireframes/investigation.html` |

Seven lanes, left to right through **Alerts / Tickets → Investigation → Approval → CAPA**: Engineer / Human, Console screen, Deterministic services, Investigation Agent, Resolution Agent, Tools / enterprise systems, Model gateway.

Violet **AGENT** badge on triage, hypothesis generation (engineering change, equipment out of calibration, bad material lot, process drift), evidence tool calls, hypothesis ranking, and the CAPA draft. **HUMAN** badge only on the ApprovalToken gate. Dashed rust arrow is the missing-or-contradictory escalation: request info, no root cause, Resolution Agent not called. Read-only tool calls are MES, QMS, PLM, ERP, CMMS, and document retrieval. After the token, the action service calls mock QMS `CreateCapa` (500 / timeout / 409). Agents do not write.

The wireframe timeline is the same step list, with chips `queued`, `running`, `done`, and `escalated`. S1 (INV-881) is done through grounding; approval and CAPA creation are queued. The S2 line (TKT-1042) shows the `escalated` chip and leaves ranking onward queued. `running` is in the chip key; this completed S1 trace has nothing in flight.

**Order choice:** the sheet draws rank, then the CAPA draft, then the grounding check. SPEC §2.5 has the Resolution Agent emit one payload, including `actions[]`, and the validator runs on that payload. The draft is its own marked step so it is visible, and it is still inside what the validator checks. The brief listed grounding before the draft. See open question 1.

Generator hook: `docs/diagrams/sheets.mjs` (`drawInvestigation`), `sheet.mjs` (swimlane band and badge), `render.mjs` (mermaid name). Regenerate with `pwsh docs/diagrams/render.ps1 -- --only investigation-flow`. Title block: concept-1 logo, G4, issue #56, v1.0, 2026-10-14, Prepared by StatusNeo, IP footer. Text is outlines. Minimum type is 14 pt.

No `DESIGN_QUESTIONS.md`. Nothing in the contracts blocked this.

---

## How verified

- `node docs/diagrams/render.mjs --only investigation-flow --check` exited 0. The checker rejects type under 14 pt, missing glyphs, node overlaps, text overlaps, arrows through nodes or labels, and interior line crossings.
- Full render exited 0. PDF MediaBox is about 2592 × 1728 pt. The PDF embeds 0 fonts. Preview PNG is 5400 × 3600.
- I looked at the full preview and at crops of the hypothesis card (four class names, AGENT badge, arrows clear of the type), the evidence-gate escalation (dashed arrow into Request info), the approval-to-CAPA elbows, the legend, and the title block (logo, G4, v1.0, 2026-10-14, StatusNeo, copyright).
- mermaid-cli (Edge) wrote `print/mermaid/investigation-flow.svg`. Auto-layout is not the plotter file.
- Headless Edge screenshots of `design/wireframes/investigation.html` at 1440×900 (full page) and 390 px wide. Timeline, four chips, eleven steps, and the existing evidence / hypothesis / trace columns all render. Mobile stacks the steps; type wraps inside the cards.
- `dotnet build` 0 warnings, 0 errors. `dotnet test` 29 passed (Domain 19, Architecture 10). `dotnet format --verify-no-changes` passed.
- No git network, no Azure CLI, no Docker, no git-credential-manager.

Not verified: a headed click-through of every wireframe control. The timeline is static HTML.

---

## Open questions for Bob

1. **Draft vs grounding order.** The sheet follows SPEC §2.5 (agent payload including `actions[]`, then the grounding validator, then the human). The brief listed grounding before the CAPA draft. Say if the arrow should be reversed.
2. **Issue id.** This brief used G4 (#56). PLAN.md §8 has no G4 row. Confirm #56 is the board issue before the PR title is fixed.
3. **Escalation chip on S1.** The path is shown as `done` with the text "not taken". `escalated` is the S2 example and the chip key. Confirm GB-4 should not invent a fifth status for "checked, not taken".
