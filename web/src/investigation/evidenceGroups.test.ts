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
