// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company
import { glossaryNote, glossarySource, glossaryTerms } from "virtual:glossary";
import type { GlossaryTerm } from "./types";

export { glossaryNote, glossarySource, glossaryTerms };

const byTerm = new Map(glossaryTerms.map((term) => [term.term.toLowerCase(), term]));

export function lookupTerm(id: string): GlossaryTerm | undefined {
  return byTerm.get(id.toLowerCase());
}

export function isAcronymTerm(term: string): boolean {
  return /[A-Z]{2,}/.test(term) || /[A-Z][a-z]+[A-Z]/.test(term) || term === "sccm" || term === "mTorr";
}
