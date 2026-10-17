// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company
import type { EvidenceItem } from "../api/types";
import { unitLabel } from "../sites/parameters";

export type EvidenceRow = {
  item: EvidenceItem;
  count: number;
  min: number | null;
  max: number | null;
  first: string;
  last: string;
  /** Distinct readings (time and value) behind the row; several rules can fire on one reading. */
  readings: number;
  /** Plain rule names, one per SPC record in the row. */
  rules: string[];
};

function readingKey(item: EvidenceItem): string {
  return `${item.observedAt}|${item.value ?? ""}`;
}

/** "Western Electric rule 2 on ESC temperature" → "Western Electric rule 2". */
function ruleOf(item: EvidenceItem): string {
  const at = item.title.lastIndexOf(" on ");
  return at > 0 ? item.title.slice(0, at) : item.title;
}

/** The parameter id from "spc:E-02:escTemperature:...", so ESC and wall temperature never share a row. */
function parameterOf(item: EvidenceItem): string {
  const parts = (item.externalRef ?? "").split(":");
  return parts.length >= 3 && parts[2] ? parts[2] : item.title;
}

function signalKey(item: EvidenceItem): string {
  if (item.source !== "spc") return `item|${item.id}`;
  return `spc|${item.toolId ?? ""}|${parameterOf(item)}`;
}

/**
 * One row per SPC signal (tool + parameter) listing the rules that broke, with real reading counts. Other evidence stays
 * one row per item. Rows run oldest to newest by the time they show (the start of a grouped row).
 */
export function groupEvidence(items: EvidenceItem[]): EvidenceRow[] {
  const rows = new Map<string, EvidenceRow>();
  const seen = new Map<string, Set<string>>();
  for (const item of items) {
    const key = signalKey(item);
    const value = typeof item.value === "number" ? item.value : null;
    const row = rows.get(key);
    const rule = item.source === "spc" ? [ruleOf(item)] : [];
    if (!row) {
      seen.set(key, new Set([readingKey(item)]));
      rows.set(key, { item, count: 1, min: value, max: value, first: item.observedAt, last: item.observedAt, readings: 1, rules: rule });
      continue;
    }
    row.count += 1;
    const keys = seen.get(key)!;
    keys.add(readingKey(item));
    row.readings = keys.size;
    for (const name of rule) if (!row.rules.includes(name)) row.rules.push(name);
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
  return [...rows.values()].sort((left, right) => timeOf(left.first) - timeOf(right.first));
}

function timeOf(stamp: string): number {
  const at = Date.parse(stamp);
  return Number.isNaN(at) ? Number.POSITIVE_INFINITY : at;
}

export function rangeText(row: EvidenceRow): string {
  if (row.count < 2) return "";
  const unit = row.item.unit ? ` ${unitLabel(row.item.unit)}` : "";
  const fmt = (v: number) => (Math.abs(v) >= 100 ? v.toFixed(0) : v.toFixed(1));
  const readings = row.readings === 1 ? "1 reading" : `${row.readings} readings`;
  const range =
    row.readings > 1 && row.min !== null && row.max !== null ? `, ${fmt(row.min)}–${fmt(row.max)}${unit}` : "";
  return row.rules.length > 1 ? `${row.rules.length} rules on ${readings}${range}` : `${readings}${range}`;
}
