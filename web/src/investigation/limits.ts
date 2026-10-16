// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company
import type { LimitState } from "../api/types";
import { unitLabel } from "../sites/parameters";

/** Wording and colour come from the server's limit state, never from a number in this file. Phrases are glossary terms. */
export const LIMIT_LABEL: Record<LimitState, string> = {
  outOfSpec: "Out of spec",
  outOfControl: "In spec, out of control",
  inControl: "In control",
};

export function limitTone(state: LimitState | null | undefined): "alarm" | "warn" | "nominal" | "muted" {
  if (state === "outOfSpec") return "alarm";
  if (state === "outOfControl") return "warn";
  if (state === "inControl") return "nominal";
  return "muted";
}

export function limitClass(state: LimitState | null | undefined): string {
  const tone = limitTone(state);
  return tone === "alarm" ? "text-mps-alarm-text" : tone === "warn" ? "text-mps-warn" : "";
}

function measure(value: number): string {
  const abs = Math.abs(value);
  if (abs >= 100) return value.toFixed(0);
  if (abs >= 10) return value.toFixed(1);
  return value.toFixed(2);
}

/** "46.0–54.0 °C", or "—" when either bound is missing. */
export function rangeLabel(low: number | null | undefined, high: number | null | undefined, unit: string | null | undefined): string {
  if (low == null || high == null || !Number.isFinite(low) || !Number.isFinite(high)) return "—";
  const label = unitLabel(unit ?? "");
  return label ? `${measure(low)}–${measure(high)} ${label}` : `${measure(low)}–${measure(high)}`;
}
