// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company
import { describe, expect, it } from "vitest";
import { bandFromEarly, buildSeries, violatesWesternElectric1, violatesWesternElectric2 } from "./series";

describe("SPC series", () => {
  it("is deterministic for a seed", () => {
    const first = buildSeries({ seed: 7, mean: 50, sigma: 1.2, shape: "nominal" });
    const second = buildSeries({ seed: 7, mean: 50, sigma: 1.2, shape: "nominal" });
    expect(first).toEqual(second);
    expect(first).toHaveLength(60);
  });

  it("keeps a nominal series inside the Western Electric 1 and 2 rules", () => {
    const points = buildSeries({ seed: 11, mean: 50, sigma: 1.2, shape: "nominal" });
    const values = points.map((point) => point.value);
    expect(violatesWesternElectric1(values, 50, 1.2)).toBe(false);
    expect(violatesWesternElectric2(values, 50, 1.2)).toBe(false);
  });

  it("draws an oscillation that trips Western Electric rule 2", () => {
    const points = buildSeries({ seed: 21, mean: 50, sigma: 1.2, shape: "oscillation" });
    const values = points.map((point) => point.value);
    expect(violatesWesternElectric2(values, 50, 1.2)).toBe(true);
  });

  it("draws a step beyond 3 sigma", () => {
    const points = buildSeries({ seed: 4, mean: 4, sigma: 0.4, shape: "step" });
    const values = points.map((point) => point.value);
    expect(violatesWesternElectric1(values, 4, 0.4)).toBe(true);
  });

  it("reads the limit from the early part of a live series", () => {
    const values = [...Array(8).fill(200), 184, 184];
    const band = bandFromEarly(values, 0, 1);
    expect(band.mean).toBeCloseTo(200);
    expect(violatesWesternElectric1(values, band.mean, band.sigma)).toBe(true);
    expect(bandFromEarly([1, 2, 3], 9, 2)).toEqual({ mean: 9, sigma: 2 });
  });
});
