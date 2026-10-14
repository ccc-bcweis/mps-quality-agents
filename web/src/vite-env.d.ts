// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company
/// <reference types="vite/client" />

declare module "virtual:glossary" {
  import type { GlossaryTerm } from "./glossary/types";

  export const glossarySource: string;
  export const glossaryNote: string;
  export const glossaryTerms: GlossaryTerm[];
}

interface ImportMetaEnv {
  readonly VITE_USE_MSW?: string;
  readonly VITE_API_BASE?: string;
}

interface ImportMeta {
  readonly env: ImportMetaEnv;
}
