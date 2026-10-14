// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company
import { ruleCodeBase } from "./terms";
import type { GlossaryTerm } from "./types";

/** Spec 2.10 terms. The glossary must contain these even when a screen does not use them yet. */
export const REQUIRED_TERMS = [
  "SPC",
  "CAPA",
  "PM",
  "OEE",
  "MTBF",
  "MTTR",
  "EC",
  "ECN",
  "UCL",
  "LCL",
  "Cpk",
  "FDC",
  "NCR",
  "COPQ",
  "RF",
  "sccm",
  "mTorr",
] as const;

/**
 * Shouted UI words and locale or protocol tokens. They are not manufacturing acronyms.
 * A new all-caps word in copy is an acronym until it is added here or to the glossary.
 */
export const ACRONYM_ALLOWLIST = new Set([
  "ALARM",
  "WATCH",
  "NOMINAL",
  "CLOSED",
  "APPROVE",
  "REPLAY",
  "LIVE",
  "MOCK",
  "MODE",
  "REASONER",
  "OK",
  "USD",
  "UTC",
  "GB",
  "US",
  "GET",
  "POST",
  "PUT",
  "DELETE",
  "PATCH",
]);

/**
 * Mixed-case short forms the spec treats as acronyms. A broad PascalCase pattern
 * also matches component names (AppShell) and hex fragments (EF4444), so new
 * mixed-case acronyms are added here and in the glossary together.
 */
const MIXED_SHAPE = /\b(?:sccm|mTorr|Cpk|IoT)\b/g;
const UPPER_SHAPE = /\b[A-Z]{2,}(?:-[A-Z0-9]+)?\b/g;

/**
 * Whole hyphenated tokens (ETCH-OX-14, CAL-ESC-02, NF3-220, ECO-4471, D-01).
 * A word boundary sits on the hyphen, so a partial mask would leave ETCH or CAL
 * for the acronym scan. WE-1 stays visible and is covered by the term WE.
 * MTTR-Q stays visible and has to be its own glossary term.
 */
const IDENTIFIER = /\b[A-Za-z][A-Za-z0-9]*(?:-[A-Za-z0-9]+)+\b/g;
const KEPT_IDENTIFIER = /^(?:WE-\d+|MTTR-Q)$/;

export function maskIdentifiers(text: string): string {
  return text.replace(IDENTIFIER, (token) => (KEPT_IDENTIFIER.test(token) ? token : " "));
}

export function findAcronymTokens(text: string): string[] {
  const masked = maskIdentifiers(text);
  const found = new Set<string>();
  for (const match of masked.matchAll(UPPER_SHAPE)) {
    const token = match[0];
    if (!ACRONYM_ALLOWLIST.has(token)) found.add(token);
  }
  for (const match of masked.matchAll(MIXED_SHAPE)) {
    const token = match[0];
    if (!ACRONYM_ALLOWLIST.has(token)) found.add(token);
  }
  return [...found];
}

export function missingAcronyms(text: string, terms: GlossaryTerm[]): string[] {
  const known = new Set(terms.map((term) => term.term));
  return findAcronymTokens(text).filter((token) => {
    if (known.has(token)) return false;
    const base = ruleCodeBase(token);
    return !base || !known.has(base);
  });
}

export function stripSourceComments(source: string): string {
  return source.replace(/\/\*[\s\S]*?\*\//g, "").replace(/(^|[^:\\])\/\/.*$/gm, "$1");
}

/** Regex literals are not UI copy. A quote inside one must not swallow the rest of the file. */
function stripRegexLiterals(source: string): string {
  return source.replace(/(^|[^A-Za-z0-9_$'"])\/(?:\\.|\[(?:\\.|[^\]])*\]|[^/\n\\])+\/[dgimsuy]*/g, "$1 ");
}

export function stringLiterals(source: string): string[] {
  const cleaned = stripRegexLiterals(stripSourceComments(source));
  const values: string[] = [];
  const pattern = /`(?:\\.|[^`\\])*`|"(?:\\.|[^"\\])*"|'(?:\\.|[^'\\])*'/g;
  for (const match of cleaned.matchAll(pattern)) {
    const raw = match[0];
    const body = raw.slice(1, -1);
    if (raw.startsWith("`")) {
      values.push(body.replace(/\$\{[^}]*\}/g, " "));
    } else {
      values.push(body);
    }
  }
  return values;
}
