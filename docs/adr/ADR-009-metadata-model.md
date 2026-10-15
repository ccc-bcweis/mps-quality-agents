# ADR-009: Plant metadata stays in files

Status: Accepted — 2026-10-15
Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

## Context

The console needs a durable description of the company, the three plants, what each plant can do, the production lines, and the tools. That description is not transactional. Alerts, tickets, investigations, and corrective actions come and go. Reset clears those records and rebuilds the live fleet. The plant description has to survive reset.

The simulator already loads `data/fleet` for recipes, sensors, and bay positions. Those files stay the simulator catalog. This work does not change the simulator, the fleet stream, or the event log.

## Decision

Metadata is loaded from `data/metadata` at startup into a read-only in-memory store (`IMetadataStore`). The load is measured and then cached for the life of the process. It is not written into the transactional database. Reset does not read or delete it.

A second copy in that database would be wiped by reset, and it would collide with the tables the event bus lane is adding at the same time. The files are small. A full load is a few milliseconds, so a database cache is not worth that risk.

Fleet files are not generated from this model. Regenerating them would drop recipes and sensors the simulator needs. Replacing the fleet loader would mean editing the simulator. Simulated tool identifiers in metadata are the same identifiers the fleet roster already uses. An extra tool that is not simulated may attach directly to a manufacturing capability, with no production line.

Business rules are data. `IBusinessRuleEngine` evaluates the first matching rule, in order, and records the rule identifier and the plain name on the decision and on the trace (`mps.rule.id`, `mps.rule.name`). The action list is open a ticket, set a priority, route to a queue, ask a person, or keep investigating. It cannot create a corrective action. An escalated investigation still cannot be approved straight into a corrective action (ADR-005). That gate stays in the human decision service.

Process maps are data too: ordered steps and a transition graph, one current-state map and one future-state map, so the console can draw them and a later runner can follow them.

## Consequences

- Other lanes call `IMetadataStore` and `IBusinessRuleEngine`. They do not parse the files themselves.
- The rules page and the sites page read the same store through the metadata API.
- Adding a plant is a file change plus the existing fleet roster, not a new code branch.
- Severity floors stay in `SeverityRules`. The engine decides whether a ticket opens, whether a person must review it, and which queue owns it.
