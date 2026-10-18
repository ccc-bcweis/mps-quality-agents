# Wireframes — demo harness and MPS console

**Issue:** DS2 (#8), updated by lane H1 (ADR-006)
**Copyright (c) 2026 Bryan C. Weis and Common Consulting Company**

Clickable static HTML. No build step. Open `index.html` in a browser (double-click or `npx serve` / `python -m http.server` from this folder). Uses `assets/tokens.css` (mirrors `design/brand/tokens.json`), Inter + JetBrains Mono inside the console, IBM Plex Sans on the harness, and logo concept 1.

The **StatusNeo demo harness** is the navy and gold band. It holds the fault injector, return on investment, metrics, the scenario picker, and Reset demo. The **Meridian Precision Systems console** is the charcoal frame under that band. Its tabs are Fleet, Tool, Alerts, Tickets, Investigation, Approval, and CAPA. The first tab position is open for Sites. Presenter tools are not console tabs.

These HTML pages still draw an old Replay / Live AI switch. The running console does not use that switch for the model. Model mode is one setting, offline, openai, or azure, shown as a chip that also says when a live call fell back to saved answers. A separate plant control can show a recorded scenario. That control does not change how the AI answers. Chart limits in the running app come from the recipe step, clamped to the sensor's physical range. The chart's horizontal axis is a fixed 600 readings.

The harness wordmark is a StatusNeo **placeholder**, not real artwork. Reset demo opens the confirmation dialog. In the app, that button is admin-only.

## Screens

| File | Layer | Flow |
|---|---|---|
| `index.html` | Application | **Fleet overview.** Eight tools, two bays, four families (MPS-D7/E7/S7/C7). D-01 alarm (S2 pressure step), E-02 watch (S1 ESC oscillation). Tiles → tool detail. |
| `tool.html` | Application | **Tool detail** for E-02 with an SPC chart. The static page draws fixed bands. The running chart uses one segment per recipe step. Link to D-01 and INV-881. |
| `alerts.html` | Application | **Alerts.** Deterministic SPC/WE rules. Auto-ticketed. A scenario run from the harness lands here with `?injected=1`. |
| `tickets.html` | Application | **Tickets.** Five intake channels (IoT, operator, NCR, supplier, injector) normalized; S1/S2 rows. |
| `investigation.html` | Application | **Investigation INV-881 (S1).** Agent-step timeline (queued / running / done / escalated) from Alerts through CAPA, then evidence pack, ranked hypotheses with `counterEvidence`, and trace. |
| `approval.html` | Application | **Approval.** Explicit confirm / reject modals. ApprovalToken rules listed. Confirm → CAPA. |
| `capa.html` | Application | **CAPA-2201.** Stages Open → … → Closed; audit log; QMS 500 retry control (visual). |
| `injector.html` | Harness | **Fault injector.** Presenter panel, not a console tab. Tool, parameter, shape, magnitude, root-cause type, confounders. Submit → alerts. Preset S4 gas-lot. |
| `roi.html` | Harness | **Return on investment.** PLAN §6.5 synthetic baseline and levers. Labeled illustrative. |
| `metrics.html` | Harness | **Metrics.** Gates from SPEC §4.3, p95, cost per investigation, confusion sketch (placeholder until E1). |

## Demo paths (for Bryan’s review)

1. **S1 success:** Fleet → E-02 → Alerts ALT-2044 → TKT-1044 → INV-881 (ECO-4471, PM confounder addressed) → Approve → CAPA-2201.
2. **S2 escalate:** Fleet → D-01 / TKT-1042. Investigation copy points at the gate: contradictory PLM vs MES, no root cause.
3. **S4 inject:** Harness bar → Fault injector → preset gas lot → Alerts shows ALT-2048.
4. **Named scenario:** Harness bar → pick a scenario → Run scenario → Alerts.
5. **Model mode:** The static pages still toggle Replay and Live AI and show a spend meter. The running app shows a model-mode chip instead. Offline saved answers cost nothing.
6. **Reset:** Harness bar → Reset demo → confirmation lists fleet state, tickets, investigations, approvals and corrective actions, and the simulated clock.

## Out of scope here

React, SignalR, real charts, MSW. The app shell in `web/` is the working version of this split.

## How to view

```
cd design/wireframes
python -m http.server 4173
# open http://127.0.0.1:4173/
```

Or open `index.html` as a file. Google Fonts need network; system UI fonts are the fallback.
