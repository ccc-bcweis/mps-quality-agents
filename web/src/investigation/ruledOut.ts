// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company
import type { EvidenceItem, Hypothesis, RejectedRuledOut, RuledOutItem, RuledOutSource } from "../api/types";

type RuledOutView = {
  id: string;
  title: string;
  ref: string | null;
  reason: string;
  sourceLabel: string | null;
};

function asList<T>(value: readonly T[] | null | undefined): readonly T[] {
  return Array.isArray(value) ? value : [];
}

function sourceLabel(source: RuledOutSource | null | undefined): string | null {
  if (source === "record") return "From the record";
  if (source === "agent") return "The AI ruled this out";
  return null;
}

/** Highest-confidence entry wins when several hypotheses rule out the same record. */
export function collectRuledOut(hypotheses: readonly Hypothesis[] | null | undefined): RuledOutItem[] {
  const ranked = [...asList(hypotheses)].sort((left, right) => (right.confidence ?? 0) - (left.confidence ?? 0));
  const seen = new Set<string>();
  const out: RuledOutItem[] = [];
  for (const hypothesis of ranked) {
    for (const entry of asList(hypothesis?.ruledOut)) {
      if (!entry || typeof entry.evidenceId !== "string" || entry.evidenceId.length === 0 || seen.has(entry.evidenceId)) continue;
      seen.add(entry.evidenceId);
      out.push(entry);
    }
  }
  return out;
}

/**
 * Records to show as considered and ruled out.
 * A record qualifies by evidence status "ruledOut" or by an id in the ruled-out list.
 * The title text is never parsed. A "Ruled out:" prefix does not qualify a record.
 * A missing list is empty.
 */
export function ruledOutViews(items: readonly EvidenceItem[] | null | undefined, ruledOut: readonly RuledOutItem[] | null | undefined): RuledOutView[] {
  const notes = new Map<string, RuledOutItem>();
  for (const entry of asList(ruledOut)) {
    if (!entry?.evidenceId || notes.has(entry.evidenceId)) continue;
    notes.set(entry.evidenceId, entry);
  }
  const views: RuledOutView[] = [];
  for (const item of asList(items)) {
    const note = notes.get(item.id);
    if (item.status !== "ruledOut" && !note) continue;
    // status ruledOut is the plant record (C9). The hypothesis note carries an agent ruling.
    const source = note?.source ?? (item.status === "ruledOut" ? "record" : null);
    views.push({
      id: item.id,
      title: item.title,
      ref: item.externalRef ?? null,
      reason: note?.reason?.trim() ?? "",
      sourceLabel: sourceLabel(source),
    });
  }
  return views;
}

/** Titles for a refused answer's ruledOutIds. A missing list is empty. Ids with no record are skipped. */
export function rejectedRuledOutViews(
  items: readonly EvidenceItem[] | null | undefined,
  ruledOutIds: readonly RejectedRuledOut[] | null | undefined,
): RuledOutView[] {
  const byId = new Map(asList(items).map((item) => [item.id, item]));
  const views: RuledOutView[] = [];
  const seen = new Set<string>();
  for (const entry of asList(ruledOutIds)) {
    if (!entry?.evidenceId || seen.has(entry.evidenceId)) continue;
    const item = byId.get(entry.evidenceId);
    if (!item) continue;
    seen.add(entry.evidenceId);
    views.push({
      id: item.id,
      title: item.title,
      ref: item.externalRef ?? null,
      reason: entry.reason?.trim() ?? "",
      sourceLabel: null,
    });
  }
  return views;
}
