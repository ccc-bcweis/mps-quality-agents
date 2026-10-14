# Design questions from GB-1 round 2

Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

Lane `lane/1b-d7`, issue D7 (#63). No contract file was edited. None of these questions block the data that shipped.

## 1. where whyItMatters lives in the agent output

**Context.** SPEC section 2.10 and ADR-003 ask the console to show a plain line for why a signal matters. D7 stores that line on the glossary, on each fault and evidence block, on each document template, and on each row of `data/evidence-matrix.yaml`. The simulator keeps the line off the enterprise records, so the draft a person sees does not gain a new field by itself. The OpenAPI and the agent output do not have the field yet.

**Options.**

1. Leave the lines in the data files. The console lane reads them directly.
2. Add an optional whyItMatters string to the agent output, and to the evidence payloads the console already shows.

**Recommendation.** Option 2 when the console lane wires the screen. The data is ready either way. This lane did not change the contract.

**Blocking?** No.

## 2. chamber id on a tool-level sensor frame

**Context.** Run rate, throughput, wafer output, and yield are properties of the tool, not of one chamber. `TelemetryFrame` requires a chamber. The simulator writes the tool id into that field and puts the catalog sensor id on the frame. The unit is the catalog unit.

**Options.**

1. Keep the tool id in the chamber field for tool-level sensors. Document it for the console.
2. Make the chamber optional on the frame. That is a contract change.

**Recommendation.** Option 1 for the demo.

**Blocking?** No.

## 3. clean tools have no RF generator

**Context.** The sensor list asks for RF forward and reflected power. The clean family is a bath with megasonic power. It has no RF generator. The fleet schema rejects an RF sensor on a clean tool. Deposition, etch, and strip carry forward power and a reflected sensor at 15 percent of the forward maximum.

**Options.**

1. Keep megasonic power on the clean tools and RF on the plasma families.
2. Add an RF generator to the clean tools so every family has the same sensor types.

**Recommendation.** Option 1. A clean tool that reports RF would be a fiction the schematic could not explain.

**Blocking?** No.

## 4. sensor spec limits versus the recipe window

**Context.** A bound sensor uses the family envelope as its minimum, maximum, and warning band. Its nominal is a nominal from a recipe window. SPC still uses the tighter recipe window. The live run-rate and yield sensors sit on a fixed spec center (12 wafers/hour, 97 percent). They are not the 90-day KPI snapshot.

**Options.**

1. Keep both bands. The schematic shows the family envelope. SPC keeps the recipe window.
2. Collapse the sensor limits to the recipe window.

**Recommendation.** Option 1. The envelope is the hardware spec. The recipe window is the process spec.

**Blocking?** No.

## 5. OEE formula for the seeded snapshot

**Context.** `data/kpis/fleet.yaml` is seed `20261014` over 2160 planned hours. Ideal run rate is 12 wafers/hour. Per tool, OEE is uptime times run rate over 12 times first-pass yield. The fleet run rate is the sum of the tool run rates. Fleet OEE uses the average tool run rate so the percent stays on the same scale. First-pass yield on the fleet row is weighted by run rate. MTBF is operating hours divided by failures. MTTR is repair hours divided by failures. This formula is not in a contract.

**Options.**

1. Keep the formula in `data/kpis/README.md` and treat the YAML as the demo snapshot.
2. Promote the formula to an ADR if the pitch deck and the console must not diverge.

**Recommendation.** Option 1 unless the deck quotes the formula as a product rule.

**Blocking?** No.
