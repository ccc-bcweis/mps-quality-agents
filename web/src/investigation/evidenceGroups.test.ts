// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company
import { describe, expect, it } from "vitest";
import type { EvidenceItem } from "../api/types";
import { groupEvidence, rangeText } from "./evidenceGroups";

function spc(id: string, value: number, at: string): EvidenceItem {
  return { id, source: "spc", title: "western-electric-1 cf4Flow", excerpt: "x", observedAt: at, toolId: "E-01", value, unit: "sccm" } as EvidenceItem;
}

describe("groupEvidence", () => {
  it("folds repeated SPC readings for one signal into one row with count and range", () => {
    const items = [
      spc("a", 31, "2026-09-02T23:50:00Z"),
      spc("b", 29.5, "2026-09-02T23:51:00Z"),
      spc("c", 33, "2026-09-02T23:52:00Z"),
      { id: "eco", source: "plm", title: "ECO", excerpt: "e", observedAt: "2026-08-01T00:00:00Z" } as EvidenceItem,
    ];
    const rows = groupEvidence(items);
    expect(rows).toHaveLength(2);
    expect(rows[0].count).toBe(3);
    expect(rows[0].item.id).toBe("c");
    expect(rangeText(rows[0])).toBe("3 readings, 29.5–33.0 sccm");
    expect(rangeText(rows[1])).toBe("");
  });
});

describe("one row per alert", () => {
  it("lists the rules that broke on one reading and keeps other parameters apart", () => {
    const at = "2026-10-16T22:16:10Z";
    const spc = (rule: string, parameter: string, value: number) => ({
      id: `${rule}-${parameter}`,
      source: "spc" as const,
      title: `${rule} on ${parameter}`,
      excerpt: "x",
      observedAt: at,
      toolId: "E-02",
      externalRef: `spc:E-02:${parameter}:${at}:${rule}`,
      value,
      unit: "C",
    });
    const rows = groupEvidence([
      spc("Western Electric rule 1", "escTemperature", 54.669),
      spc("Western Electric rule 2", "escTemperature", 54.669),
      spc("Western Electric rule 4", "wallTemperature", 59.9),
    ]);
    expect(rows).toHaveLength(2);
    expect(rangeText(rows[0])).toBe("2 rules on 1 reading");
    expect(rows[0].rules).toEqual(["Western Electric rule 1", "Western Electric rule 2"]);
  });
});
