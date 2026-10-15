# Brand kit — MPS Quality Agents

**Issue:** DS1 (#7)
**Copyright (c) 2026 Bryan C. Weis and Common Consulting Company**
**License:** MIT

This kit has **two layers**. They never mix on the same surface except at the documented seams (deck title plate, and the StatusNeo demo harness bar that frames the console). ADR-006 records that split.

| Layer | Owner of the surface | Used on |
|---|---|---|
| **StatusNeo pitch shell** | StatusNeo presenting | PowerPoint framing, title/closing slides, “Prepared for…” lockup, demo harness bar |
| **Meridian Precision Systems** | The fictional customer | The demo app: a dark fab operations console |

Bryan presents **as StatusNeo** pitching to **Meridian Precision Systems**. The console is MPS’s own control room, with “Powered by StatusNeo” in the footer.

Tokens live in [`tokens.json`](tokens.json) and [`tailwind.theme.js`](tailwind.theme.js). Logo concepts are the three SVGs in this folder; compare them on [`logo-sheet.html`](logo-sheet.html).

---

## 1. StatusNeo pitch shell

### 1.1 Role
Consulting-firm framing for the deck and for any “this is a StatusNeo proposal” chrome. Light paper or navy title plates. Warm gold accent. Calm, boardroom, not a fab.

### 1.2 Wordmark (placeholder)
**Do not use StatusNeo’s real logo artwork in this repo.** Until Bryan confirms official-asset use, the pitch shell is a **text wordmark**:

- Type: **IBM Plex Sans 700**, tracking `0.04em`, color navy `#0B1B33` on paper or gold `#FAC400` on navy.
- Optional gold underline, 2 px, width of the word, offset 4 px.
- In layouts, reserve a 120 × 32 px dashed box labeled `STATUSNEO LOGO (official artwork TBD)` next to the wordmark. See `logo-sheet.html`.

Gold `#FAC400` is taken from StatusNeo’s public website CSS (accent used on statusneo.com). It is a color reference, not a copy of their mark.

### 1.3 Palette

| Token | Hex | Role | Contrast (WCAG 2) |
|---|---|---|---|
| `sn.navy` | `#0B1B33` | Title plates, footer bar | White on navy **17.23:1 AAA** |
| `sn.navySoft` | `#1A2744` | Secondary navy | — |
| `sn.gold` | `#FAC400` | Accent, rules, underlines | Gold on navy **10.63:1 AAA**. **Fails AA on white (1.62:1)** — never body text on paper |
| `sn.goldInk` | `#7A5C00` | Gold-colored text on paper | **6.25:1 AA** on white |
| `sn.goldWash` | `#F3E8C2` | Gold wash on navy | **14.06:1 AAA** |
| `sn.paper` | `#F7F4EA` | Slide background | — |
| `sn.ink` | `#111827` | Body on paper/white | **17.74:1 AAA** on white |
| `sn.muted` | `#4B5563` | Captions | **7.56:1 AAA** on white |
| `sn.white` | `#FFFFFF` | Reverse text | — |

### 1.4 Typography
Free/open fonts only (SIL OFL).

| Role | Face | Weights | Source |
|---|---|---|---|
| Pitch sans (default) | **IBM Plex Sans** | 400 / 500 / 600 / 700 | [Google Fonts](https://fonts.google.com/specimen/IBM+Plex+Sans) |
| Pitch serif (optional titles) | **IBM Plex Serif** | 400 / 600 | [Google Fonts](https://fonts.google.com/specimen/IBM+Plex+Serif) |

Deck title 36/600, section 22/600, body 16/400, caption 12/400, footer 10/400. Line height 1.15–1.45.

### 1.5 Voice on pitch surfaces
- Line: “Prepared for Meridian Precision Systems.”
- Thesis (from SPEC): “Engineers decide; AI assembles and reasons over evidence; rules enforce.”
- Footer on every slide: “© 2026 Bryan C. Weis and Common Consulting Company. All intellectual property owned by Bryan C. Weis / Common Consulting Company. Fictional demonstration, AI-assisted; all companies and data are synthetic.”

---

## 2. Meridian Precision Systems (customer / console)

### 2.1 Character
A highly instrumented semiconductor **demo fab** running thin-film deposition, plasma etch, photoresist strip, and single-wafer clean. The UI is a **dark control-room console**: charcoal canvas, steel panels, dense tiles, sparklines, SPC charts with ±1/2/3σ bands, status LEDs, a lifecycle tracker. It should feel like a tool on a bay, not a marketing site.

Fictional tool families (SPEC §2.0): **MPS-D7** deposition, **MPS-E7** plasma etch, **MPS-S7** photoresist strip, **MPS-C7** single-wafer clean. Eight tools, two bays. Never use a real equipment-vendor name.

### 2.2 Palette

| Token | Hex | Role | Contrast on `#0E1116` |
|---|---|---|---|
| `mps.canvas` | `#0E1116` | App background | — |
| `mps.panel` | `#1A1F27` | Cards, tiles, nav | Text `#F8FAFC` on panel **15.81:1 AAA** |
| `mps.raised` | `#252B35` | Chart wells, hover | — |
| `mps.line` | `#3D4654` | Borders, grid | — |
| `mps.text` | `#F8FAFC` | Primary text | **18.08:1 AAA** |
| `mps.textSecondary` | `#E5E7EB` | Secondary | **15.27:1 AAA** |
| `mps.muted` | `#94A3B8` | Meta, axes | **7.38:1 AAA** (do not use gray-500; it fails AA) |
| `mps.nominal` | `#22D3EE` | In-control, live, selected | **10.46:1 AAA** |
| `mps.nominalSoft` | `#67E8F9` | Sparklines, σ1 | **13.05:1 AAA** |
| `mps.warn` | `#F59E0B` | Warning, approaching limit | **8.81:1 AAA** |
| `mps.alarm` | `#EF4444` | Alarm LED, σ3, open incident | **5.03:1 AA** (use `#FECACA` for small alarm copy) |
| `mps.alarmSolid` | `#B91C1C` | Solid alarm button | White on this fill **6.47:1 AA** |
| `mps.ok` | `#10B981` | Approved, closed | **7.46:1 AAA** |
| `mps.okSolid` | `#047857` | Solid approve button | White on this fill **5.48:1 AA** |
| `mps.ai` | `#A78BFA` | Agent / probabilistic traces | Distinct from cyan rules |

**Pairing rules**

- Body text is always `#F8FAFC` or `#E5E7EB` on canvas/panel.
- Status color **and** a text label (never color alone).
- White on `#EF4444` is **AA-large only (3.76:1)**. Alarm buttons use `alarmSolid` `#B91C1C` with white, or ink `#0E1116` on `#EF4444`.
- Deterministic logic (SPC, gates, state machine) is cyan. Probabilistic AI (agents, hypotheses) is violet. Do not recode agents as green.

Full pair table including StatusNeo: `tokens.json` → `contrast.pairs`.

### 2.3 Typography
Free/open fonts only (SIL OFL).

| Role | Face | Weights | Source |
|---|---|---|---|
| UI | **Inter** | 400 / 500 / 600 / 700 | [Google Fonts](https://fonts.google.com/specimen/Inter) |
| Values, IDs, traces | **JetBrains Mono** | 400 / 500 / 600 | [Google Fonts](https://fonts.google.com/specimen/JetBrains+Mono) |

Ramp (dense console): display 22/600 Inter, title 16/600 Inter, body 13/400 Inter, label 11/500 Inter + 0.04em, value 13/500 Mono, KPI 20/600 Mono, micro 10/500 Mono.

### 2.4 Layout language
- 4 px grid. Tight padding (12–16 px in tiles).
- Panels: 4 px radius, 1 px `#3D4654` border, `shadow.panel`.
- Status LED: 8 px circle, 2 px glow of the same color.
- Lifecycle bar on every operational console screen: Alert → Ticket → Investigation → Recommendation → Approval → CAPA. It is not shown on a harness panel.
- The mode line (live / replay / mock reasoner) and the spend meter live in the StatusNeo demo harness bar, not in the console tab row.
- Console footer: the IP line. The StatusNeo wordmark placeholder lives in the harness bar.

### 2.5 Motion
120 ms color/opacity, 200 ms panels. No decorative animation. Charts update; they do not bounce.

---

## 3. Logo concepts (MPS)

Bryan picks one. All three are original geometry; none copy a vendor mark. Open [`logo-sheet.html`](logo-sheet.html) to compare on dark and light.

### Concept 1 — True Meridian (`logo-concept-1.svg`)
A wafer disc with SEMI-style notch, a faint die grid, a metrology reticle, and a **vertical meridian through the origin**. Reads as alignment, inspection, and “true north” for quality. Best default for a fab console: it looks like an overlay a process engineer already trusts. Scales to 16 px (diamond + ring still parse).

**Use if:** the console should feel like metrology and SPC, not like a consumer brand.

### Concept 2 — Four Films (`logo-concept-2.svg`)
Four isometric process films stacked into an **M**: deposition (cyan), etch, strip (amber), clean (green). The cut face is a wafer edge. Encodes the four tool families in one mark. More “product brand,” slightly less “HMI icon.”

**Use if:** the pitch needs a memorable family story (four modules, one line). Weaker at 16 px because the layers collapse.

### Concept 3 — Chamber Viewport (`logo-concept-3.svg`)
A steel chamber bezel, RF coil arcs, wafer on a chuck, status LED. Reads as **a tool**, not a letter. Strongest “we run equipment” signal. The LED can be recolored to nominal/warn/alarm in-product.

**Use if:** the demo leans into the Fault Injector and live fleet. Slightly busier in nav; use the LED-off variant in small sizes.

### Clear space and minimum size
- Clear space: 0.25 × mark height on all sides.
- Minimum: 24 px for concept 1; 32 px for 2 and 3.
- On light backgrounds, keep the charcoal plate (`#0E1116` rounded square) or invert strokes to `#0B1B33` with cyan `#0891B2` (verify contrast; cyan `#22D3EE` on white is for large/graphics, not small text).
- Do not add drop shadows, gradients beyond the films in concept 2, or a real-vendor silhouette.

### StatusNeo lockup
On the deck title: StatusNeo **placeholder wordmark** (left) + gold rule + “Prepared for Meridian Precision Systems” (right). On the running demo: StatusNeo placeholder in the harness bar, MPS mark in the console header. Never stack the two marks in the same corner.

---

## 4. Do / don’t

**Do**
- Label every status with text + LED.
- Put IDs (`TKT-1042`, `ECO-4471`, `E-02`) in JetBrains Mono.
- Keep LIVE / REPLAY / MOCK banners honest; never style replay as live.
- State “illustrative, synthetic” on ROI numbers.
- Carry the IP footer on pitch slides.

**Don’t**
- Name a real equipment vendor, or use a lookalike tool name.
- Use **AURA** (trademark; excluded).
- Ship StatusNeo logo artwork from the public site.
- Set body copy in gold `#FAC400` on white.
- Use green for AI traces (green means approved/closed).
- Lighten the console to a marketing landing page.

---

## 5. Name collision notes (for Bryan / Bob)

A 2026-10-14 web search found **no exact match** for “Meridian Precision Systems” as a company. Residual risk on the **standalone** words:

- **Meridian** appears in existing semiconductor failure-analysis *product* names.
- **MPS** is used by unrelated electronics and precision-mechanics firms.

SPEC already adopted the full name after an earlier check. Recommendation: always introduce **Meridian Precision Systems** in full, then “MPS”; avoid “Meridian” alone on slides that could be screenshotted out of context. Tool families stay `MPS-D7 / E7 / S7 / C7` as frozen in SPEC. Flag to Bryan if a tighter name-clearance pass is wanted before the review.

---

## 6. How GB-4 consumes this

```js
// web/tailwind.config.js (illustrative)
const mps = require("../design/brand/tailwind.theme.js");
module.exports = {
  content: ["./index.html", "./src/**/*.{ts,tsx}"],
  theme: { extend: mps.extend },
};
```

CSS variables for the static wireframes: `../wireframes/assets/tokens.css` (mirrors this file). Fonts load from Google Fonts in HTML prototypes; the React app should vendor or use the same families.

**Open decision:** Bryan picks concept 1, 2, or 3 (or a hybrid: concept 1 mark + concept 2 family legend). Until then, wireframes use **concept 1**.
