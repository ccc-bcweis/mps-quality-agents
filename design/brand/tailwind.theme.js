/**
 * Tailwind theme fragment for MPS Quality Agents.
 * Spread into theme.extend (Tailwind 3) or @theme (Tailwind 4).
 *
 *   // tailwind.config.js
 *   const mps = require('../../design/brand/tailwind.theme.js');
 *   module.exports = { theme: { extend: mps.extend } };
 *
 * Copyright (c) 2026 Bryan C. Weis and Common Consulting Company
 * SPDX-License-Identifier: MIT
 */

const extend = {
  colors: {
    sn: {
      navy: "#0B1B33",
      "navy-soft": "#1A2744",
      gold: "#FAC400",
      "gold-ink": "#7A5C00",
      "gold-wash": "#F3E8C2",
      paper: "#F7F4EA",
      ink: "#111827",
      muted: "#4B5563",
    },
    mps: {
      canvas: "#0E1116",
      panel: "#1A1F27",
      raised: "#252B35",
      line: "#3D4654",
      text: "#F8FAFC",
      secondary: "#E5E7EB",
      muted: "#94A3B8",
      nominal: "#22D3EE",
      "nominal-soft": "#67E8F9",
      warn: "#F59E0B",
      "warn-soft": "#FCD34D",
      alarm: "#EF4444",
      "alarm-text": "#FECACA",
      "alarm-solid": "#B91C1C",
      ok: "#10B981",
      "ok-solid": "#047857",
      "info-solid": "#1D4ED8",
      ai: "#A78BFA",
    },
  },
  fontFamily: {
    sans: ["Inter", "Segoe UI", "system-ui", "sans-serif"],
    mono: ["JetBrains Mono", "Cascadia Code", "Consolas", "ui-monospace", "monospace"],
    pitch: ["IBM Plex Sans", "Segoe UI", "Helvetica Neue", "Arial", "sans-serif"],
    "pitch-serif": ["IBM Plex Serif", "Georgia", "Times New Roman", "serif"],
  },
  fontSize: {
    "mps-micro": ["10px", { lineHeight: "1.2", letterSpacing: "0.04em" }],
    "mps-label": ["11px", { lineHeight: "1.3", letterSpacing: "0.04em" }],
    "mps-body": ["13px", { lineHeight: "1.4" }],
    "mps-title": ["16px", { lineHeight: "1.3" }],
    "mps-display": ["22px", { lineHeight: "1.2" }],
    "mps-kpi": ["20px", { lineHeight: "1.1" }],
  },
  spacing: {
    1: "4px",
    2: "8px",
    3: "12px",
    4: "16px",
    5: "20px",
    6: "24px",
    8: "32px",
    10: "40px",
    12: "48px",
    16: "64px",
  },
  borderRadius: {
    sm: "2px",
    DEFAULT: "4px",
    md: "4px",
    lg: "8px",
    pill: "999px",
  },
  boxShadow: {
    panel: "0 1px 0 rgba(255,255,255,0.04), 0 8px 24px rgba(0,0,0,0.35)",
    overlay: "0 16px 48px rgba(0,0,0,0.55)",
  },
  zIndex: {
    sticky: "20",
    banner: "30",
    nav: "40",
    overlay: "50",
    toast: "60",
  },
  transitionDuration: {
    fast: "120ms",
    DEFAULT: "200ms",
  },
  transitionTimingFunction: {
    mps: "cubic-bezier(0.2, 0.8, 0.2, 1)",
  },
};

module.exports = {
  extend,
  /** Convenience: full theme.extend object for Tailwind 3 configs. */
  theme: { extend },
};
