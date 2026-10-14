// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company
import { glossaryNote, glossarySource, glossaryTerms } from "virtual:glossary";
import type { GlossaryTerm } from "./types";

export { glossaryNote, glossarySource, glossaryTerms };

const byTerm = new Map(glossaryTerms.map((term) => [term.term.toLowerCase(), term]));

/** Western Electric rule ids (WE-1) use the glossary term WE. The digits are the rule suffix. */
const RULE_CODE = /^(WE)-(\d+)$/i;

export function ruleCodeBase(id: string): string | undefined {
  const match = RULE_CODE.exec(id);
  return match ? "WE" : undefined;
}

export function lookupTerm(id: string): GlossaryTerm | undefined {
  const direct = byTerm.get(id.toLowerCase());
  if (direct) return direct;
  const base = ruleCodeBase(id);
  return base ? byTerm.get(base.toLowerCase()) : undefined;
}

export function isAcronymTerm(term: string): boolean {
  return /[A-Z]{2,}/.test(term) || /[A-Z][a-z]+[A-Z]/.test(term) || term === "sccm" || term === "mTorr";
}
