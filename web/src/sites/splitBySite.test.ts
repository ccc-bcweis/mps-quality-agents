// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company
import { describe, expect, it } from "vitest";
import { splitBySite } from "./splitBySite";

const rows = [{ tool: "E-01" }, { tool: "I-E-01" }, { tool: undefined }];
const toolFor = (row: { tool?: string }) => row.tool;

describe("CAPA banner count", () => {
  it("never reports anything elsewhere under All sites", () => {
    const split = splitBySite(rows, "all", toolFor);
    expect(split.elsewhere).toBe(0);
    expect(split.visible).toHaveLength(3);
  });

  it("counts only rows at another plant, from the same list as the rows", () => {
    const split = splitBySite(rows, "northwater", toolFor);
    expect(split.visible.map((row) => row.tool)).toEqual(["E-01"]);
    expect(split.elsewhere).toBe(1);
    expect(split.visible.length + split.elsewhere).toBeLessThanOrEqual(rows.length);
  });
});
