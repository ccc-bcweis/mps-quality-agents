// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company
import { describe, expect, it } from "vitest";
import { splitGlossary } from "./split";
import { lookupTerm } from "./terms";
import { samplesOutOfLimits } from "../pages/AlertsPage";

describe("plain rule names link to the glossary", () => {
  it("links Nelson rule 6 and Western Electric rules", () => {
    const terms = splitGlossary("Western Electric rules 1-4 and Nelson rule 6 (rising trend)").filter((p) => p.kind === "term");
    expect(terms.map((p) => (p.kind === "term" ? p.term : ""))).toEqual(["Western Electric rules", "Nelson rule 6"]);
    expect(lookupTerm("Nelson rule 6")?.term).toBe("Nelson rule 6");
    expect(lookupTerm("Nelson rules")?.term).toBe("Nelson rules");
    expect(lookupTerm("Nelson rule 3")?.term).toBe("Nelson rules");
  });
  it("counts samples, not checks", () => {
    expect(samplesOutOfLimits(1)).toBe("1 sample out of limits");
    expect(samplesOutOfLimits(12)).toBe("12 samples out of limits");
  });
});

import { plainParameters } from "../sites/parameters";
describe("parameter ids in text", () => {
  it("become display names", () => {
    expect(plainParameters("The cf4Flow shift on E-01 and o2Flow")).toBe("The CF4 flow shift on E-01 and O2 flow");
    expect(plainParameters("E-01-A-MFC-CF4 stays")).toBe("E-01-A-MFC-CF4 stays");
  });
});

describe("misleading records glossary entry", () => {
  it("explains the injector switch", () => {
    expect(lookupTerm("misleading records (to rule out)")?.definition).toMatch(/not the cause/);
  });
});
