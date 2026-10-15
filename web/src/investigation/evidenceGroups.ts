// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company
import type { EvidenceItem } from "../api/types";

export type EvidenceRow = {
  item: EvidenceItem;
  count: number;
  min: number | null;
  max: number | null;
  first: string;
  last: string;
};

function signalKey(item: EvidenceItem): string {
  if (item.source !== "spc") return `item|${item.id}`;
  const parameter = item.title.trim().split(/\s+/).pop() ?? item.title;
  return `spc|${item.toolId ?? ""}|${item.signalId ?? ""}|${parameter}`;
}

/** One row per SPC signal (tool + parameter) with a count and value range. Other evidence stays one row per item. */
export function groupEvidence(items: EvidenceItem[]): EvidenceRow[] {
  const rows = new Map<string, EvidenceRow>();
  for (const item of items) {
    const key = signalKey(item);
    const value = typeof item.value === "number" ? item.value : null;
    const row = rows.get(key);
    if (!row) {
      rows.set(key, { item, count: 1, min: value, max: value, first: item.observedAt, last: item.observedAt });
      continue;
    }
    row.count += 1;
    if (value !== null) {
      row.min = row.min === null ? value : Math.min(row.min, value);
      row.max = row.max === null ? value : Math.max(row.max, value);
    }
    if (item.observedAt < row.first) row.first = item.observedAt;
    if (item.observedAt >= row.last) {
      row.last = item.observedAt;
      row.item = item;
    }
  }
  return [...rows.values()];
}

export function rangeText(row: EvidenceRow): string {
  if (row.count < 2) return "";
  const unit = row.item.unit ? ` ${row.item.unit}` : "";
  const fmt = (v: number) => (Math.abs(v) >= 100 ? v.toFixed(0) : v.toFixed(1));
  const range = row.min !== null && row.max !== null ? `, ${fmt(row.min)}–${fmt(row.max)}${unit}` : "";
  return `${row.count} readings${range}`;
}
