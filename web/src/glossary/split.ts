// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company
import { glossaryTerms } from "./terms";
import type { GlossaryTerm } from "./types";

export type GlossaryPart = { kind: "text"; text: string } | { kind: "term"; term: string };

function escapeRegExp(value: string): string {
  return value.replace(/[.*+?^${}()|[\]\\]/g, "\\$&");
}

let cachedKey = "";
let cachedPattern: RegExp | null = null;

function patternFor(terms: GlossaryTerm[]): RegExp | null {
  if (terms.length === 0) return null;
  const key = terms.map((term) => term.term).join("\n");
  if (cachedPattern && cachedKey === key) return cachedPattern;
  const sorted = [...terms].sort((a, b) => b.term.length - a.term.length);
  const body = sorted.map((term) => escapeRegExp(term.term)).join("|");
  cachedKey = key;
  cachedPattern = new RegExp(`(?<![A-Za-z0-9-])(?:${body})(?![A-Za-z0-9-])`, "g");
  return cachedPattern;
}

export function splitGlossary(text: string, terms: GlossaryTerm[] = glossaryTerms): GlossaryPart[] {
  const pattern = patternFor(terms);
  if (!pattern) return [{ kind: "text", text }];
  pattern.lastIndex = 0;
  const parts: GlossaryPart[] = [];
  let cursor = 0;
  for (const match of text.matchAll(pattern)) {
    const index = match.index ?? 0;
    if (index > cursor) parts.push({ kind: "text", text: text.slice(cursor, index) });
    parts.push({ kind: "term", term: match[0] });
    cursor = index + match[0].length;
  }
  if (cursor < text.length) parts.push({ kind: "text", text: text.slice(cursor) });
  if (parts.length === 0) return [{ kind: "text", text }];
  return parts;
}
