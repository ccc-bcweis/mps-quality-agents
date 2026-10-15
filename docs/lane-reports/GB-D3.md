# Lane GB-D3

Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

Branch `lane/d3`. Commits are local. Nothing was pushed.

## Done

Architecture diagrams and the deck outline now use the three fictional plants from lane S1, and they separate the StatusNeo harness from the Meridian console.

| Plant | Role on the drawings |
|---|---|
| Northwater Fab | Front-end fab. The floor plan and the MX-400 etch example sit here. Scenario SC-5 starts here. |
| Ironwood Fab | Second front-end fab. Etch runs hotter. The same SC-5 lot reaches this plant. |
| Lumen Pilot Works | Strip, clean, and a pilot line. Not on the SC-5 lot. |

What each requested drawing now shows:

| Drawing | Plants | Sites API | Harness vs console | Escalation gate | Scenario evidence |
|---|---|---|---|---|---|
| Logical | Intake card and the impact card | Intake card | Harness card, console card | Write path | Corpus card and the resolution card |
| Physical | Three cards under the resource group | API card | Static Web App card | API card | Mock-enterprise card |
| Investigation flow | Ticket card | Narrative and the mermaid | Console lane subtitle | Approval cards | Enterprise-reads card and the rank card |
| To-be flow | Operator card and the glossary line | Mermaid | Operator card | Mermaid, and the glossary line | Mermaid |

The site map (`docs/diagrams/mps-sites.svg`) and the fab floor use the same names. `plants.mjs` is the shared list. Earlier proposal names are gone from those generators.

`docs/PLAN.md` section 7 is a 13-slide outline. Slide 1 is the company overview and the site map. Later slides name the screenshot and whether it is the harness, the console, or neither. Return on investment stays on the harness. The escalation gate and the corrective-and-preventive-action screen stay on the console.

No new glossary terms. The short forms on the sheets were already in `data/glossary.yaml`.

## How it was verified

`pwsh -File docs/diagrams/render.ps1 -- --check` exited 0. That run includes the overlap self-test (overlap, clip, and drawing-ink) and then validates every sheet, the site map, and both picture frames. No label overlap and no clipped label.

`pwsh -File docs/diagrams/render.ps1 -- --only <slug>` exited 0 for `logical`, `physical`, `investigation-flow`, `to-be-flow`, and `fab-floor`. Each of those wrote an outlined ARCH D PDF and a 5400 by 3600 preview PNG. Mermaid-cli rewrote `print/mermaid/<slug>.svg` for the four flows. Fab floor has no mermaid source. The site map is SVG only. The check already rewrote it. Unchanged sheets (`cicd`, `as-is-flow`, `as-is-vs-to-be`) stayed byte-identical.

I looked at the five previews. Plant names, the harness line, the escalation gate, and the scenario-evidence line are readable. The south chase on the floor plan fits on one line.

`dotnet test tests/Simulator.Tests` passed: 56 tests, 0 failed. That includes the glossary scan of `docs/`. Web tests were not run. This lane did not change `web/` or `data/`.

No git network commands. No Azure CLI.

## Open questions for Bob

1. The Sites tab is still not on this branch. Slide 2 says to use the site map until that screen exists, and not to fake a console crop.
2. The title-block date is still 2026-10-14. It is one shared string. Say if these sheets should carry 2026-10-15.
3. On the logical sheet the escalation gate took the slot that used to say mock quality system. The investigation sheet still shows the quality write, including a forced failure. Say if the logical sheet should grow a fourth write card.
4. Mermaid-cli output is the trace render. Those drawings are wide, and auto-layout can cross edges. The overlap check applies to the hand-composed sheets and the site map, which passed.
