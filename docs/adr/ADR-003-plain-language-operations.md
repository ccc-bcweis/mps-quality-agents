# ADR-003: Plain-language operations

Status: Accepted (Bryan Weis, product owner; recorded by Bob, architect) — 2026-10-14
Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

## Context
The console is aimed at fab engineers, but the people who escalate, fund and sign off on fixes are often supervisors and managers who aren't engineers. Manufacturing quality tools usually speak in acronyms, raw tag names and control-chart jargon, so only specialists can read them.

## Decision
Plain language is a deliberate design principle across the product (SPEC 2.10):
1. **Glossary-driven terms.** Every term is spelled out on first use. Every acronym has a dotted underline with a hover/focus tooltip giving a plain definition, all driven by one file, `data/glossary.yaml`. There's also a Glossary page, and a test fails the build if the UI uses an acronym that isn't in the glossary (UX1 #59).
2. **Machine schematics.** Each tool is shown as an SVG schematic of its subsystems and sensor points, with live values, units and status colors. The sensor catalog in the fleet data drives it (UX2 #60, D7 #63).
3. **Why-it-matters evidence.** Every evidence item comes with one plain sentence on why it matters. An evidence matrix links each root-cause class to the signals that support it or rule it out (UX3 #61).
4. **Plain-word metrics.** Run rate, uptime/OEE, yield and MTBF/MTTR appear with plain labels and tooltips (UX4 #62).

## Rationale
A supervisor or manager who isn't an engineer can understand and explain a problem, because nothing is in code: no raw tag names, unexplained acronyms or untranslated numbers. That shortens escalation and approval, makes the human approval gate a real decision instead of a rubber stamp, and builds trust in the AI's reasoning, because every claim is tied to a signal the reader can understand.

## Consequences
- The agents' output contract includes `whyItMatters` for each cited evidence item, and the grounding validator checks it.
- The data lane owns `glossary.yaml` and the sensor catalog. The UI lane owns rendering.
- There's an executive deck slide on this principle (K5 #66).
