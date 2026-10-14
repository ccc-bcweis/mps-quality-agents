// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company
import { describe, expect, it } from "vitest";
import { TOOL_PERFORMANCE, formatHours, formatPercent, formatRate, performanceFor, rollup } from "./performance";

describe("plain-language performance", () => {
  it("averages the eight tools instead of storing a second fleet total", () => {
    expect(TOOL_PERFORMANCE.map((row) => row.toolId)).toEqual(["D-01", "D-02", "E-01", "E-02", "S-01", "S-02", "C-01", "C-02"]);
    const mean = (pick: (row: (typeof TOOL_PERFORMANCE)[number]) => number) =>
      TOOL_PERFORMANCE.reduce((sum, row) => sum + pick(row), 0) / TOOL_PERFORMANCE.length;
    const fleet = rollup();
    expect(fleet.runRate).toBeCloseTo(mean((row) => row.runRate), 10);
    expect(fleet.runRate).toBeCloseTo(26.4375, 5);
    expect(fleet.uptime).toBeCloseTo(94, 5);
    expect(fleet.oee).toBeCloseTo(78.375, 5);
    expect(fleet.firstPassYield).toBeCloseTo(96.6875, 5);
    expect(fleet.mtbfHours).toBeCloseTo(210, 5);
    expect(fleet.mttrHours).toBeCloseTo(2.8, 5);
    expect(formatRate(fleet.runRate)).toBe("26.4 wafers/h");
    expect(formatPercent(fleet.oee)).toBe("78.4%");
    expect(formatPercent(fleet.uptime)).toBe("94.0%");
    expect(formatPercent(fleet.firstPassYield)).toBe("96.7%");
    expect(formatHours(fleet.mtbfHours, 0)).toBe("210 h");
    expect(formatHours(fleet.mttrHours, 1)).toBe("2.8 h");
    expect(formatRate(performanceFor("E-02")?.runRate ?? 0)).toBe("24.0 wafers/h");
    expect(formatRate(performanceFor("D-01")?.runRate ?? 0)).toBe("22.0 wafers/h");
    expect(formatPercent(performanceFor("D-01")?.oee ?? 0)).toBe("71.0%");
  });
});
