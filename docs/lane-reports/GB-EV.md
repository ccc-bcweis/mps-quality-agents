# Lane report: evidence and the agent trace

Branch `lane/ev`. Issue #112. Local commits only. Nothing was pushed.

Copyright (c) 2026 Bryan C. Weis and Common Consulting Company. MIT.

## Done

Evidence on Investigation, Approval, CAPA, Tickets, and Alerts is a table: source in plain words, one line for what it shows, the reading against the spec, a local time such as Oct 14, 10:08 AM, and why it matters. The full text sits behind Details. The agent trace is a step timeline with a name, a one-line summary, and a duration. The prompt and the tool payload sit behind Details.

Record ids stay on a hover title or in the audit log. Tool and chamber names read as "Etch tool E-02, Chamber B". The etch temperature row uses the fleet chart's last point: 53.1 °C, spec 40-60 °C. The deposition pressure row reads 6.52 Torr, spec 2-6 Torr.

Optional evidence fields `value`, `unit`, `specLow`, `specHigh`, `toolName`, and `chamberName` were already on the console investigation response. OpenAPI `info.version` stays 1.2.0. The event schema is unchanged. The contract addendum is in `docs/adr/ADR-001-contracts.md`.

Tickets and Alerts keep their list tables. The evidence pack and the timeline are added under the list. The console frame CSS was not edited. The approval escalation gate is unchanged: an escalated case still needs more evidence or a written override.

| Commit | What |
|---|---|
| `1bf44f9` | Optional reading fields and plain equipment names in the API. |
| `59e0e4c` | Formatting helpers and their tests. |
| `318ab52` | Evidence table and agent timeline on Investigation. |
| `b9bbb9f` | Same pack and timeline on Approval, CAPA, Tickets, and Alerts. |
| `717385d` | Contract note heading, so the glossary scan stays clean. |
| `50880d7` | Etch temperature against its spec in the demo pack. |
| `cd8626b` | An approval is remembered, so the CAPA timeline shows that decision. |

`47ce86d` is the earlier partial save. The commits above keep the useful part of that work.

## How it was verified

`dotnet build` succeeded with 0 warnings. `dotnet format` made no changes. `dotnet test` passed: Domain 22, ModelGateway 15, Application 176, Agents 77, MockEnterprise 4, Connectors 7, Orchestration 15, Architecture 14, Simulator 56, Contract 39, Api 26. Api.IntegrationTests matched no tests under the existing `Category!=PendingLane` filter and did not fail.

From `web/`: `npm test` (33 files, 91 tests) and `npx tsc --noEmit` both exited 0. The new tests cover the formatters and check that Investigation, Approval, Tickets, Alerts, and CAPA do not render a raw record id or a machine timestamp in the operator copy. Details and the audit log are excluded from that check. The CAPA token is still the exact text of the audit-log token.

Headless Edge at 1280, 1600, and 1920 opened Investigation, Approval, Tickets, Alerts, and CAPA (after Approve). A measurement of headings, table cells, links, and buttons found no overflow and no overlapping labels. The evidence table shows the temperature against the spec, and the CAPA timeline marks human approval done. The content column stays at its existing 1200px cap, so the table wraps the same way at all three widths.

No git network command was run. Azure CLI was not used.

## Open questions for Bob

- Step durations are illustrative. The API does not return a trace with real spans. Blocking: no.
- Alert and ticket list cells still use the existing UTC stamp. The new local time is on the evidence table under the list. Blocking: no.
- The CAPA audit log still shows the token and the incident id. That is the audit log, not the operator summary. Blocking: no.
