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
    const spcRow = rows.find((row) => row.item.source === "spc")!;
    expect(spcRow.count).toBe(3);
    expect(spcRow.item.id).toBe("c");
    expect(rangeText(spcRow)).toBe("3 readings, 29.5–33.0 sccm");
    expect(rangeText(rows.find((row) => row.item.source === "plm")!)).toBe("");
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

describe("evidence order", () => {
  it("runs oldest to newest whatever order the records arrive in", () => {
    const items = [
      { id: "run2", source: "mes", title: "Run 2", excerpt: "r", observedAt: "2026-10-12T05:00:00Z" } as EvidenceItem,
      spc("a", 31, "2026-10-17T06:55:00Z"),
      { id: "eco", source: "plm", title: "ECO", excerpt: "e", observedAt: "2026-10-01T00:00:00Z" } as EvidenceItem,
      { id: "run1", source: "mes", title: "Run 1", excerpt: "r", observedAt: "2026-10-05T11:00:00Z" } as EvidenceItem,
      spc("b", 29, "2026-10-17T06:54:00Z"),
    ];
    expect(groupEvidence(items).map((row) => row.item.id)).toEqual(["eco", "run1", "run2", "a"]);
  });
});
