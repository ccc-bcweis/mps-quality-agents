# Demo screens

What a presenter shows, and which layer it belongs to. ADR-006 is the decision. The clickable wireframes are in `design/wireframes`.

Copyright (c) 2026 Bryan C. Weis and Common Consulting Company. MIT.

## StatusNeo demo harness

Navy and gold band above the product. Not part of the Meridian Precision Systems console.

| Screen | Where | What the presenter uses it for |
|---|---|---|
| Harness bar | Every screen | StatusNeo wordmark, fault injector, return on investment, metrics, scenario picker, Reset demo (admin only), live / replay / mock, today's spend against the daily cap. |
| Fault injector | `/harness/injector` | Free-form fault. Named scenarios run from the bar, then the console opens on alerts. |
| Return on investment | `/harness/roi` | Illustrative savings sketch. Every figure is synthetic. |
| Metrics | `/harness/metrics` | How this demo run is scoring, and what it has spent. |

## Meridian Precision Systems console

Charcoal application inside the gold frame.

| Screen | Where | Notes |
|---|---|---|
| Sites | `/sites` | Three plants. The tab sits before Fleet. |
| Fleet | `/` | Eight tools, two bays. |
| Tool | `/tools/:toolId` | Schematic and the process window. |
| Alerts | `/alerts` | Rules-engine alerts, newest first. A missing alert falls back to the list. A scenario run lands here. |
| Tickets | `/tickets` | Normalized intake, newest first. Operator report and Upload telemetry sit side by side. A missing ticket falls back to the list. |
| Investigation | `/investigations` | The tab opens the list, newest first. A missing deep link falls back to the list. Detail stays at `/investigations/:id`. |
| Approval | `/approvals` | The tab opens the list. A missing deep link falls back to the list. The gate stays at `/approvals/:id`. |
| Corrective and preventive action | `/capas` | The tab opens the list, newest first. Detail stays at `/capas/:id`. |
| Glossary | `/glossary` | Application page, linked from the console header. Not a tab, and not a harness tool. |

On Tickets, choose a tool and the parameter list shrinks to that tool's sensor channels. The label shows the plain name, the unit, and the sensor spec (etch ESC temperature is 40-60 C). Upload telemetry accepts a PDF or a PNG. The extracted reading can be corrected. Confirm stores it as evidence. A reading outside the printed spec also opens an alert and moves the simulation. The oscilloscope sample is `data/samples/oscilloscope-e01.pdf`. ADR-007.

## What not to mix

Reset demo, the scenario picker, the fault injector, return on investment, and metrics are presenter tools. They are not console tabs. Deck crops follow `docs/PLAN.md` section 7.
