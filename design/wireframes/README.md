# Wireframes — MPS Fab Ops Console

**Issue:** DS2 (#8)
**Copyright (c) 2026 Bryan C. Weis and Common Consulting Company**

Clickable static HTML. No build step. Open `index.html` in a browser (double-click or `npx serve` / `python -m http.server` from this folder). Uses `assets/tokens.css` (mirrors `design/brand/tokens.json`), Inter + JetBrains Mono, and logo concept 1.

Mode banner (LIVE AI / REPLAY) and the **today's spend** meter are on every screen. Toggle is local to the prototype (`localStorage`). Footer is the StatusNeo **wordmark placeholder**, not real artwork.

## Screens

| File | Flow |
|---|---|
| `index.html` | **Fleet overview.** Eight tools, two bays, four families (MPS-D7/E7/S7/C7). D-01 alarm (S2 pressure step), E-02 watch (S1 ESC oscillation). Tiles → tool detail. |
| `tool.html` | **Tool detail** for E-02 with SPC chart (±1/2/3σ bands, Western Electric oscillation). Windows from SPEC §2.1. Link to D-01 and INV-881. |
| `alerts.html` | **Alerts.** Deterministic SPC/WE rules. Auto-ticketed. Injector lands here with `?injected=1`. |
| `tickets.html` | **Tickets.** Five intake channels (IoT, operator, NCR, supplier, injector) normalized; S1/S2 rows. |
| `investigation.html` | **Investigation INV-881 (S1).** Three columns: evidence pack (IDs), ranked hypotheses with `counterEvidence`, trace (rules vs AI vs human). |
| `approval.html` | **Approval.** Explicit confirm / reject modals. ApprovalToken rules listed. Confirm → CAPA. |
| `capa.html` | **CAPA-2201.** Stages Open → … → Closed; audit log; QMS 500 retry control (visual). |
| `injector.html` | **Fault injector.** Tool, parameter, shape, magnitude, root-cause type, confounders. Submit → alerts. Preset S4 gas-lot. |
| `roi.html` | **ROI.** PLAN §6.5 synthetic baseline and levers. Labeled illustrative. |
| `metrics.html` | **Metrics / eval.** Gates from SPEC §4.3, p95, cost per investigation, confusion sketch (placeholder until E1). |

## Demo paths (for Bryan’s review)

1. **S1 success:** Fleet → E-02 → Alerts ALT-2044 → TKT-1044 → INV-881 (ECO-4471, PM confounder addressed) → Approve → CAPA-2201.
2. **S2 escalate:** Fleet → D-01 / TKT-1042. Investigation copy points at the gate: contradictory PLM vs MES, no root cause.
3. **S4 inject:** Injector → preset gas lot → Alerts shows ALT-2048.
4. **Mode / spend:** Banner toggle LIVE ↔ REPLAY; spend meter $1.24 / $2.00 in LIVE, $0.00 in REPLAY.

## Out of scope here

React, SignalR, real charts, MSW. GB-4 implements U1–U4 against this look. DS3 (hi-fi Fleet + Investigation) is a later design issue.

## How to view

```
cd design/wireframes
python -m http.server 4173
# open http://127.0.0.1:4173/
```

Or open `index.html` as a file. Google Fonts need network; system UI fonts are the fallback.
