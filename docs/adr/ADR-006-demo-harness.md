# ADR-006: Demo harness vs application

Status: Accepted (Bryan Weis, product owner; recorded by lane H1) — 2026-10-14
Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

## Context
The pitch has two audiences looking at one screen. StatusNeo is presenting. Meridian Precision Systems is the fictional customer whose operators would use the product. The first build put both jobs in one console: fleet, tool, alerts, tickets, investigation, approval, and corrective and preventive action sat in the same tab row as the fault injector, the return-on-investment sketch, the metrics page, the reset control, and the live / replay switch.

A buyer who sees "Reset demo" or "Fault injector" next to "Approval" can think those controls ship with the product. They do not. They exist so a presenter can run the story.

## Decision
The screen is two layers. They stay visually separate. Presenter controls never appear as application tabs.

**StatusNeo demo harness (outer band).** Navy and gold, the pitch palette, not the charcoal console. It wraps the application. It holds:

- The StatusNeo wordmark placeholder (official artwork is not in this repo).
- Fault injector, return on investment, and metrics. Each opens a harness panel at `/harness/injector`, `/harness/roi`, or `/harness/metrics`. The old paths `/injector`, `/roi`, and `/metrics` redirect there.
- Reset demo, for an admin only, with the existing confirmation of what is restored.
- The scenario picker. Running a named scenario leaves the harness and opens the application's alerts.
- The mode line: live, replay, or mock reasoner, plus today's spend against the daily cap from the metrics endpoint.

A harness panel is labeled as a presenter tool. It is not a console screen.

**Meridian Precision Systems console (inner application).** Charcoal, cyan status, the customer's mark. It holds only the live application tabs, in this order:

1. Sites, when that tab exists. Until then the nav leaves the first position open for it.
2. Fleet, Tool, Alerts, Tickets, Investigation, Approval, and corrective and preventive action.

The glossary stays an application page (plain-language operations). It is linked from the console header, not from the harness, and it is not a console tab. The lifecycle bar stays on console screens only. No harness control is rendered inside the console region.

## Rationale
A buyer must never confuse presenter controls with product features. Reset, scripted scenarios, and the fault injector are how StatusNeo starts a story. The return-on-investment panel is an illustrative sales sketch. The metrics panel and the spend line show how the demo itself is running. An operator in a fab would not inject a fake fault, reset the plant, or watch the model's daily spend from the same row as Approve.

The color split does the same job as the tab split. Navy and gold mean "this is the pitch." Charcoal and cyan mean "this is the product."

## Consequences
- Shell and navigation render the harness bar above the console. Injector, return on investment, and metrics are no longer console tabs.
- Plain-language rules still apply to every label on both layers. Acronyms keep glossary tooltips.
- Wireframes, the demo screen list, and the deck screenshot plan name which shots are the harness and which are the application. Deck shots may keep the harness bar in frame so the split is visible. They must not crop it off and then treat a harness control as a product button.
- The brand kit's old seam (StatusNeo only in the console footer) is replaced by this outer band. See `design/brand/BRAND.md`.
