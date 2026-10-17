# Demo screens

What a presenter shows, and which layer it belongs to. ADR-006 is the decision. The clickable wireframes are in `design/wireframes`.

Copyright (c) 2026 Bryan C. Weis and Common Consulting Company. MIT.

## StatusNeo demo harness

Navy and gold band above the product. Not part of the Meridian Precision Systems console.

| Screen | Where | What the presenter uses it for |
|---|---|---|
| Harness bar | Every screen | One row until the presenter opens it: a down arrow and a line for plant data, the scenario, and the answer engine. The choice stays in this browser. Opened, the bar holds scenario cards, the fault injector, a sample printout upload, hard and soft reset (admin only), plant data Live or Replay, the answer engine, return on investment, metrics, and the audit log. The open bar stays within about a third of a short screen. The console scrolls on its own. |
| Fault injector | `/harness/injector` | Free-form fault. Named scenarios run from the bar, then the console opens that tool with the trace streaming. |
| Return on investment | `/harness/roi` | Illustrative savings sketch. Every figure is synthetic. |
| Metrics | `/harness/metrics` | How this demo run is scoring, and what it has spent. |
| Audit log | `/harness/audit` | Every step with who did it and when: alerts, tickets, agent steps, decisions, CAPA stages, resets. |
| Join links | Harness bar, admin only | Create, copy, list and revoke expiring, use-limited links. A guest who redeems one at `/join/:token` becomes a full user (ADR-011). |

## Meridian Precision Systems console

Charcoal application inside the gold frame.

| Screen | Where | Notes |
|---|---|---|
| Sites | `/sites` | Three plants. The tab sits before Production line. |
| Production line | `/` | Eight tools, two bays. |
| Tool | `/tools/:toolId` | Schematic and the process window. |
| Alerts | `/alerts` | Rules-engine alerts, newest first. Detail at `/alerts/:alertId`. A missing alert falls back to the list. |
| Tickets | `/tickets` | Normalized intake, newest first. Detail at `/tickets/:ticketId`. Operator report and Upload telemetry sit side by side. A missing ticket falls back to the list. |
| Investigation | `/investigations` | The tab opens the list, newest first. A missing deep link falls back to the list. Detail stays at `/investigations/:id`. |
| Decision | `/investigations/:id#decision` | No tab of its own: the Decision section is on the Investigation page. Old links redirect: `/approvals` → `/investigations`, `/approvals/:id` → `/investigations/:id#decision`. Request more evidence, and any round waiting for more evidence, shows **Add evidence** (text, plus an optional bench printout). Submitting closes the round and the agents run the next one. **Rounds on this ticket** lists Round 1, Round 2 and so on: past rounds are locked history with their decision, and only the current round has buttons. Opening a past round says "This is round N, kept as history" with a link to the current round. |
| Corrective and preventive action | `/capas` | The tab opens the list, newest first. Detail stays at `/capas/:id`. |
| Rules & Process | `/rules` | Thresholds and settings (including the most rounds per ticket for Add evidence, 5), each business rule's conditions and actions, and one process map of the actual flow with its numbers filled from those settings. Edit and Save show the fields but persist nothing. |
| Glossary | `/glossary` | Application page, linked from the console header. Not a tab, and not a harness tool. |

On Tickets, choose a tool and the parameter list shrinks to that tool's sensor channels. The label shows the plain name, the unit, and the sensor spec (etch ESC temperature is 40-60 C). Upload telemetry accepts a PDF or a PNG. The extracted reading can be corrected. Confirm stores it as evidence. A reading outside the printed spec also opens an alert and moves the simulation. The oscilloscope sample is `data/samples/oscilloscope-e01.pdf`. ADR-007.

## Before sign-in

| Page | Where | Notes |
|---|---|---|
| Landing | `/landing.html` | Hosted only. A signed-out browser lands here and sees a **Log in** button. After **Sign out** it reads as signed out. An account with no access sees "Signed in as X, which has no access" and **Use a different account**. |

## What not to mix

Hard reset, soft reset, the scenario picker, plant-data replay, the fault injector, return on investment, and metrics are presenter tools. They are not console tabs. A replay banner on the console only says that the numbers on screen are a recording. Deck crops follow `docs/PLAN.md` section 7.
