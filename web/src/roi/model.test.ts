// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company
import { describe, expect, it } from "vitest";
import { projectRoi } from "./model";

describe("ROI model", () => {
  const roi = projectRoi();

  it("reproduces the PLAN §6.5 baseline", () => {
    expect(roi.copqUsd).toBe(96_000_000);
    expect(roi.laborUsd).toBe(1_400 * 22 * 95);
    expect(roi.scrapUsd + roi.warrantyUsd + roi.downtimeUsd + roi.laborUsd).toBe(96_000_000);
  });

  it("projects a conservative and an expected savings range", () => {
    expect(roi.conservativeSavingsUsd).toBe(4_368_075);
    expect(roi.expectedSavingsUsd).toBe(7_599_195);
    expect(roi.modelRunCostUsd).toBe(84);
    expect(roi.hypothesisDaysExpected).toBeCloseTo(1.2);
    expect(roi.capaDaysExpected).toBeCloseTo(40.3);
  });
});
