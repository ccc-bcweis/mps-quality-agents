# Design questions — GB-D round 3b

Copyright (c) 2026 Bryan C. Weis and Common Consulting Company.

The investigation sheet (G4, v1.1) was drawn against SPEC 2.10 and SPEC §2.4. Contract files were not changed. Neither question blocks the diagram.

## 1. `whyItMatters` is not in contracts-v1

**Context.** SPEC 2.10 and ADR-003 require agent output to include `whyItMatters` on each cited evidence item, and the grounding validator checks that the line cites a real evidence id. `contracts/openapi.yaml` `EvidenceItem` is `additionalProperties: false` and requires only `id`, `source`, `title`, `excerpt`, `observedAt`. `Hypothesis` requires `id`, `category`, `cause`, `confidence`, `evidenceIds`, `counterEvidence`, `actions`. Neither schema has `whyItMatters`. The same lists are in `contracts/events/hypothesis-proposed.schema.json`. The print sheet shows where the lines are written (Investigation Agent, after the reads, before the evidence gate) and where they are checked (grounding). It does not invent a wire shape.

**Options.**

1. Add a required `whyItMatters` string on `EvidenceItem`. Connectors would have to fill it. The sentence is agent prose, not a fact returned by manufacturing, quality, or maintenance systems.
2. Keep `EvidenceItem` as the raw record. Add the lines on the evidence pack, one string per evidence id, written before ranking. Require every `Hypothesis.evidenceIds` entry to have a line. Grounding rejects a citation whose line is missing or whose id is not in the pack.
3. Leave the contracts frozen. The console invents a sentence from `title` and `excerpt` until the next contract revision. The agent never emits the field, so grounding cannot check it.

**Recommendation.** Option 2, when contracts next open. The line is produced before the Resolution Agent runs, so it should not wait for `Hypothesis`. **Blocking?** No for this diagram. Yes for the agent and UI work that has to persist and check the field.

## 2. Sheet label "Out of spec" and the header "Signals that rule it out"

**Context.** SPEC §2.4 names the class "Equipment out of spec/calibration/alignment". SPEC 2.10 names it "equipment out of spec". The sheet uses **Out of spec** and puts the calibration meaning in the support cell ("Overdue calibration or alignment, on one chamber"). The brief asked for signals that support or rule a class out. SPEC §2.4 calls the same second signal the typical confounder: it competes with the class, and it does not always eliminate it. The sheet header says **Signals that rule it out** because that is the plain phrase in the brief. The engineering note in `docs/diagrams/investigation-flow.md` says the column is the confounder.

**Options.**

1. Keep the short class label and the "rule it out" header. The support cell carries the calibration words. The narrative tells a reviewer the column is the confounder.
2. Rename the class cell to "Calibration or alignment" and the header to "Competing signal". Longer, and closer to §2.4, with less room in the row.
3. Use the §2.4 header "Typical confounder" on the plotter sheet.

**Recommendation.** Option 1. The pitch reader gets the brief's words, and the support cell stops "out of spec" from meaning only a control-limit breach. **Blocking?** No.
