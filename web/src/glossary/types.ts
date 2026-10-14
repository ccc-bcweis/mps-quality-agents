// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

export type GlossaryTerm = {
  term: string;
  expansion: string;
  definition: string;
  whyItMatters?: string;
};

export type GlossaryFile = {
  copyright?: string;
  sourceNote?: string;
  terms: GlossaryTerm[];
};
