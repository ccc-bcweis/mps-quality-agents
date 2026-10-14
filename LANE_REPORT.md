# Lane report — GB-D (design + diagrams)

**Branch:** `lane/d-design`  
**Agent:** Grok Build  
**Date:** 2026-10-14  
**Scope:** DS1 (#7), DS2 (#8), G1 (#46), G2 (#47). No application code. DS3 (hi-fi Fleet + Investigation) is P1 and was not in this brief.

Copyright (c) 2026 Bryan C. Weis and Common Consulting Company.

---

## Done per issue

### DS1 (#7) — `design/brand/`
Commit `176a22b` `feat(DS1): add StatusNeo pitch shell and MPS brand kit (#7)`

- `BRAND.md` — two layers: **StatusNeo pitch shell** (deck framing, navy/gold, IBM Plex, wordmark **placeholder**) and **Meridian Precision Systems** customer console (dark fab ops, SPEC palette, Inter + JetBrains Mono).
- Palette with hex and WCAG 2 contrast ratios (AA/AAA). Gold `#FAC400` documented as **accent only** (fails AA on white). Alarm buttons use `#B91C1C` for white text.
- `tokens.json` and `tailwind.theme.js` for GB-4.
- Three distinct SVG concepts: **1 True Meridian** (wafer + reticle, recommended default), **2 Four Films** (process stack M), **3 Chamber Viewport** (tool bezel + LED).
- `logo-sheet.html` — all three on dark and light, plus StatusNeo dashed placeholder. **No StatusNeo logo artwork.**

### DS2 (#8) — `design/wireframes/`
Commit `49cdd3e` `feat(DS2): add clickable fab console wireframes (#8)`

Clickable static HTML (no build). Shared nav, lifecycle bar, LIVE/REPLAY banner, spend meter (`$0.00` replay / `$1.24 of $2.00` live), StatusNeo footer placeholder.

| Screen | File |
|---|---|
| Fleet overview | `index.html` |
| Tool detail + SPC | `tool.html` (`?id=D-01` shows S2 callout) |
| Alerts | `alerts.html` (`?injected=1` shows S4 row) |
| Tickets | `tickets.html` |
| Investigation (evidence, hypotheses, trace) | `investigation.html` |
| Approval (confirm / reject modals) | `approval.html` |
| CAPA | `capa.html` |
| Fault injector | `injector.html` |
| ROI | `roi.html` |
| Metrics | `metrics.html` |

Flows documented in `design/wireframes/README.md` (S1, S2, S4, mode/spend).

### G1 (#46) — `docs/diagrams/logical.md`
Commit `5894074`

Mermaid logical architecture with explicit bands: deterministic (SPC, intake, lifecycle, impact, evidence gate, ApprovalToken, action service), probabilistic (MAF orchestrator, Investigation Agent, Resolution Agent), model gateway (Azure OpenAI / OpenAI / Replay / mock reasoner, ledger, spend cap), eval/governance (grounding validator, eval runner, red-team, audit), mock enterprise (MES/QMS/PLM/ERP/CMMS/ticketing), document retrieval. Legend + narrative.

### G2 (#47) — `docs/diagrams/physical.md`
Commit `85d7156`

Mermaid Azure demo: Entra invite-only (assignment required), managed identity, Key Vault, Container Apps environment as network boundary, SQL serverless, Azure OpenAI, Static Web App, App Insights/OTel, GitHub Actions CI + OIDC gated deploy. Scaling notes: API p95 &lt; 300 ms, investigation p95 &lt; 45 s, unit cost ≈ $0.02–0.10 per investigation, $2/day hard cap.

---

## How verified

- **No git network, no az, no Docker run, no git-credential-manager.** Local commits only.
- Contrast ratios computed with WCAG relative luminance (script, not guessed).
- Trademark/name web search: no exact “Meridian Precision Systems” company; residual risk on standalone “Meridian” and “MPS” (documented in BRAND.md without naming real equipment vendors). **AURA** is only in a do-not-use line.
- HTML: every local `href`/`src` resolves; all ten screens include mode banner, spend meter, and StatusNeo placeholder footer.
- Local `python -m http.server 4173` from repo root: HTTP 200 on all wireframe pages, CSS/JS, logos, logo-sheet.
- Headless Edge screenshots (1440×900) of fleet, tool+SPC, investigation, injector, logo-sheet. Visual check: dark console, cyan/amber/red status, concept-1 mark, banner, spend meter, S1 investigation columns, injector form.
- Mermaid: one fenced `flowchart` per file, no HTML `<br>` in node labels, no `end` node ids, `classDef` on G1. **Not** rendered on GitHub in this session.

Not verified: live GitHub mermaid renderer; clicking every control in a headed browser (no browser MCP). Mode toggle and inject navigation are small JS; they were not exercised beyond first-paint screenshots.

---

## Open questions for Bob

1. **Logo pick (needs Bryan):** concept 1 (default in wireframes), 2, or 3? Blocks GB-4 polish, not plumbing.
2. **StatusNeo artwork (needs Bryan):** keep dashed wordmark, or may we add official SVG after OK? Do not scrape the public site.
3. **Name clearance:** residual collision on the words “Meridian” and “MPS”. SPEC already adopted the full name. Confirm before the review deck goes out.
4. **G1/G2 issue numbers:** this brief used #46 and #47. Confirm they match the GitHub Project issues once the board is up (PLAN.md table does not list G1/G2 rows).
5. **DS3:** hi-fi Fleet + Investigation is the next design issue after Bryan signs these wireframes. Not started.
6. **Azure OpenAI quota / region:** still unchecked (no Azure CLI). Not this lane; blocks Z2.

GB-4 can consume `design/brand/tailwind.theme.js` and treat the wireframes as the U1–U4 layout reference.
