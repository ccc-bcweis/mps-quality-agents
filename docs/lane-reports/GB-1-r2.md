# Lane report: GB-1 round 2, D7

Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

Branch `lane/1b-d7`. Local commits only. Nothing was pushed. Contract files, domain types, and the logo were not changed. Design questions are in `docs/design-questions/GB-1-r2.md`. None of them block D7.

| Issue | Commit | What landed |
|---|---|---|
| D7 (#63) sensors | `8b0c12f` | Each of the eight tools has a `sensors` catalog: id, type, unit, subsystem, schematic anchor, and spec limits. The set covers thermocouples, chamber pressure, gas flow, vibration, run rate, throughput, output, and yield. Deposition, etch, and strip also list RF forward and reflected power. Clean tools list megasonic power and do not list RF. The simulator emits exactly those ids and units. |
| D7 (#63) glossary | `8ace8df` | `data/glossary.yaml` has a term, expansion, plain definition, and one-line whyItMatters for every acronym and unit the docs and data files use, including SPC, CAPA, PM, OEE, MTBF, MTTR, EC, ECN, UCL, LCL, Cpk, FDC, NCR, COPQ, RF, MFC, sccm, and mTorr. |
| D7 (#63) KPIs | `2a30442` | Seed `20261014` writes run rate, uptime, OEE, first-pass yield, MTBF, and MTTR per tool and for the fleet. The formula is in `data/kpis/README.md`. The committed `data/kpis/fleet.yaml` matches the generator. |
| D7 (#63) explainability | `c94d621` | Each fault, each evidence block, and each of the twelve document templates has a one-line whyItMatters. `data/evidence-matrix.yaml` maps the four root-cause classes to signals that support a class or rule it out. |

## How it was verified

On this machine, before the explainability commit, with the working tree in the state those commits snapshot together:

- `dotnet build MPS.sln` succeeded with 0 warnings. Warnings are errors.
- `dotnet test MPS.sln` passed Simulator 47, Domain 19, Application 158, MockEnterprise 4, Connectors 7, Architecture 10, Contract 39, and Api 11. `Api.IntegrationTests` is excluded by its own `Category!=PendingLane` filter, so those tests did not run.
- `dotnet format MPS.sln --verify-no-changes` exited 0.

What the new tests lock:

- Every tool carries the required sensor types. A sensor unit that disagrees with its type is rejected. Plasma tools have reflected power inside the forward-power envelope. Clean tools have no RF sensor. Over one recipe cycle the simulator emits each catalog id once per tick pattern and no other id, and the unit on the frame is the catalog unit. Tool output counts wafers inside the simulated hour.
- The glossary loads, terms are unique, and a scan of `docs/` and `data/` (markdown, YAML, and CSV, skipping print, fonts, and the glossary file itself) finds no missing acronym or unit.
- The same KPI seed redraws the same file. Run rate stays between 10.80 and 11.76 wafers/hour. An empty tool list is rejected. For seed `20261014` the fleet row is 89.76 wafers/hour, 94.25 percent uptime, 84.42 percent OEE, 95.80 percent first-pass yield, MTBF 465.31 hours, and MTTR 5.05 hours.
- Each evidence block has a plain whyItMatters of at most 220 characters. Those lines are not copied into the enterprise draft. A drift fault that also carries an engineering change is still rejected. The evidence matrix gives every class at least two supporting signals and one signal that rules it out, and a signal cannot both support and rule out the same class.
- The twelve rendered documents contain whyItMatters. The untrusted bay note still contains the injection sentence. `tools/Ingest` rewrote the corpus. The search index bytes did not change, and the connector test still matches the committed index. Ground-truth JSON was not regenerated.

## Open questions for Bob

- Should the agent output grow a whyItMatters field, or should the console keep reading these data files? See design question 1. Not blocking.
- Tool-level frames put the tool id in the chamber field because the frame requires a chamber. See design question 2.
- Clean tools list megasonic power. See design question 3.
- Sensor spec limits are the family envelope. Recipe windows stay in place for SPC. See design question 4.
- The OEE product is a lane assumption written in `data/kpis/README.md`. See design question 5.
