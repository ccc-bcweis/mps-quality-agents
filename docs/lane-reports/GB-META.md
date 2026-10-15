# Lane report

Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

Issues 127 and 128. Branch `lane/bv2-meta`. Commits are local. Nothing was pushed.

| Step | Commit | What landed |
|---|---|---|
| Catalog and rules | `759f19b` | Plant metadata files, JSON schemas, the read-only store, and the business rules engine. |
| API | `c983759` | Company, sites, tools, rules, and process maps. OpenAPI 1.4.0. Reset leaves the catalog in place. |
| Console | `19beb91` | Rules and Process tab, process-map diagrams, and the site hierarchy on Sites. |

## Metadata

The company, three sites, capabilities, production lines, tools, rules, and process maps live in `data/metadata`. Northwater Fab, Ironwood Fab, and Lumen Pilot Works are the three plants. Each site has manufacturing, warehousing, research and development, sales, back office, and other. Only manufacturing is simulated.

A production line is the process order: photoresist strip, clean, thin-film deposition, then etch. An end-to-end line names those four steps and holds no tools. Tools keep the same identifiers the fleet roster already uses, so scenarios and evidence still match. One extra tool, the film check bench, attaches directly to Northwater manufacturing and is not simulated.

The fleet files are unchanged. The simulator still loads them. This lane does not edit the simulator, the fleet stream, or the event log.

`IMetadataStore` reads the files once at startup, caches them, and records how long the load took. The catalog is not written into the transactional database. Reset does not read or delete it. ADR-009 records that choice. `docs/architecture/data-model.md` draws the metadata entities beside the transactional ones: telemetry readings, the event log, alerts, incidents, tickets, investigations, evidence, approvals, corrective actions, scenarios, and commands.

JSON schemas in `contracts/metadata` check each file. A test also checks referential integrity: every tool line exists, every cited parameter exists, limits sit inside one another, and only the last rule may leave its condition empty.

## Rules

`IBusinessRuleEngine` evaluates the first matching rule. Conditions can name the alert rule, severity, parameter class, repeat count, site, tool, investigation confidence, and whether the evidence is enough. Actions open a ticket, set a priority, route to a queue, ask a person, or keep the investigation going. A rule cannot open a corrective action.

Intake and the SPC alert path call the engine instead of the old hard-coded ticket and escalation branches. Today's scenario patterns still open the same tickets and still ask a person in the same cases. Each decision keeps the rule identifier and the plain name, and the trace records them. An escalated investigation still cannot go straight to a corrective action. That gate stays in the human decision service (ADR-005).

## Console

`GET /api/metadata/company`, `/api/metadata/sites`, `/api/metadata/tools/{id}`, `/api/metadata/rules`, and `/api/metadata/process-maps` are additive.

The Rules and Process tab is inside the Meridian console. The table is when, then, and why, with no rule identifiers on screen. The maps draw the current path and the future path. Choosing a ticket highlights the step that matches where that case sits. Sites keeps the floor, then lists site, capabilities, production lines, and tools. Capabilities that are not simulated say so. The glossary adds Capability and Production line.

Step labels are checked at 1280, 1600, and 1920 widths so they do not overlap or clip. That check is a geometry test. It was not repeated in a browser.
