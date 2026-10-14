// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company
import { describe, expect, it } from "vitest";
import { parseGlossaryYaml } from "./parse";
import { splitGlossary } from "./split";
import type { GlossaryTerm } from "./types";

const sample = `
# comment
terms:
  - term: SPC
    expansion: statistical process control
    definition: A running chart.
    whyItMatters: Supervisors can see the alarm.
  - term: "mTorr"
    expansion: millitorr
    definition: A small pressure unit.
  - term: WE-1
    expansion: Western Electric rule 1
    definition: One point beyond three sigma.
`;

const terms: GlossaryTerm[] = [
  { term: "SPC", expansion: "statistical process control", definition: "A running chart." },
  { term: "ECO", expansion: "engineering change order", definition: "A numbered change." },
  { term: "WE-1", expansion: "Western Electric rule 1", definition: "One point." },
  { term: "WE", expansion: "Western Electric", definition: "Pattern rules." },
];

describe("glossary parse and split", () => {
  it("reads a flat terms list and skips comments", () => {
    const parsed = parseGlossaryYaml(sample);
    expect(parsed.map((term) => term.term)).toEqual(["SPC", "mTorr", "WE-1"]);
    expect(parsed[0]?.whyItMatters).toMatch(/Supervisors/);
    expect(parsed[1]?.whyItMatters).toBeUndefined();
  });

  it("keeps identifiers intact and splits standalone terms", () => {
    const parts = splitGlossary("ECO-4471 changed the ring. SPC flagged WE-1.", terms);
    expect(parts.filter((part) => part.kind === "term").map((part) => part.term)).toEqual(["SPC", "WE-1"]);
    expect(parts.map((part) => (part.kind === "text" ? part.text : part.term)).join("")).toBe(
      "ECO-4471 changed the ring. SPC flagged WE-1.",
    );
  });
});
