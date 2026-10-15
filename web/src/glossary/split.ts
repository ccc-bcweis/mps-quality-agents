// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company
import { glossaryTerms, isAcronymTerm } from "./terms";
import type { GlossaryTerm } from "./types";

export type GlossaryPart = { kind: "text"; text: string } | { kind: "term"; term: string };

function escapeRegExp(value: string): string {
  return value.replace(/[.*+?^${}()|[\]\\]/g, "\\$&");
}

let cachedKey = "";
let cachedPattern: RegExp | null = null;

function patternFor(terms: GlossaryTerm[]): RegExp | null {
  // Units and plain words stay on the glossary page. "rev C" is a revision letter, not Celsius.
  const linkable = terms.filter((term) => isAcronymTerm(term.term) && term.term !== "Western Electric rules");
  const rules = terms.some((term) => term.term === "WE");
  if (linkable.length === 0 && !rules) return null;
  const key = `${terms.some((term) => term.term === "Western Electric rules") ? "WER|" : ""}${linkable.map((term) => term.term).join("\n")}|${rules ? "WE-#" : ""}`;
  if (cachedPattern && cachedKey === key) return cachedPattern;
  const sorted = [...linkable].sort((a, b) => b.term.length - a.term.length);
  const parts = sorted.map((term) => escapeRegExp(term.term));
  if (rules) parts.unshift("WE-\\d+");
  // "Western Electric rule 2" and "Western Electric rules" both open the Western Electric rules definition.
  if (terms.some((term) => term.term === "Western Electric rules")) parts.unshift("Western Electric rules?");
  const body = parts.join("|");
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
