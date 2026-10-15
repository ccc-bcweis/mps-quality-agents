// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company
import type { GlossaryTerm } from "./types";

const FIELDS = ["term", "expansion", "definition", "whyItMatters"] as const;

function clean(value: string): string {
  const trimmed = value.trim();
  if (
    (trimmed.startsWith('"') && trimmed.endsWith('"')) ||
    (trimmed.startsWith("'") && trimmed.endsWith("'"))
  ) {
    return trimmed.slice(1, -1);
  }
  return trimmed;
}

/** Flat glossary YAML: a `terms:` list of term, expansion, definition, and optional whyItMatters. */
export function parseGlossaryYaml(source: string): GlossaryTerm[] {
  const terms: GlossaryTerm[] = [];
  let current: Partial<GlossaryTerm> | null = null;

  const flush = () => {
    if (!current) return;
    if (!current.term || !current.expansion || !current.definition) {
      throw new Error(`Glossary entry is missing term, expansion, or definition (${current.term ?? "no term"}).`);
    }
    terms.push({
      term: current.term,
      expansion: current.expansion,
      definition: current.definition,
      whyItMatters: current.whyItMatters,
    });
    current = null;
  };

  for (const raw of source.split(/\r?\n/)) {
    const line = raw.replace(/\t/g, "  ");
    if (/^\s*$/.test(line) || /^\s*#/.test(line)) continue;
    const item = line.match(/^\s*-\s+term:\s*(.*)$/);
    if (item) {
      flush();
      current = { term: clean(item[1] ?? "") };
      continue;
    }
    const field = line.match(/^\s+(term|expansion|definition|plainDefinition|whyItMatters):\s*(.*)$/);
    const fieldName = field?.[1] === "plainDefinition" ? "definition" : field?.[1];
    if (field && current && FIELDS.includes(fieldName as (typeof FIELDS)[number])) {
      const key = fieldName as keyof GlossaryTerm;
      current[key] = clean(field[2] ?? "");
      continue;
    }
  }
  flush();
  return terms;
}
