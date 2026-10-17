// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company
import { describe, expect, it } from "vitest";
import { familyComparison, siteDetails } from "../mocks/sites/fixtures";
import { isKnownParameter } from "../labels";
import { labelFits, labelInside, layoutFloor, overlappingPairs, scaleLayout, statusWord } from "./floorLayout";

const WIDTHS = [1248, 1280, 1600, 1888, 1920];

describe("factory floor labels", () => {
  it("keeps every label inside the floor and clear of the others from 1280 to 1920", () => {
    for (const site of siteDetails) {
      const layout = layoutFloor(site);
      const toolLabels = layout.labels.filter((label) => label.kind === "tool");
      expect(toolLabels.length).toBe(site.bays.reduce((sum, bay) => sum + bay.tools.length, 0));
      for (const bay of site.bays) {
        for (const tool of bay.tools) {
          const label = toolLabels.find((item) => item.id === `tool-${tool.id}`);
          expect(label?.text).toBe(`${statusWord(tool.status)}\n${tool.id}`);
        }
      }
      expect(layout.labels.some((label) => label.kind === "flow")).toBe(true);

      for (const width of WIDTHS) {
        const scaled = scaleLayout(layout, width);
        const margin = 8 * (width / layout.width);
        expect(overlappingPairs(scaled.labels), `${site.id} at ${width}`).toEqual([]);
        const problems = scaled.labels
          .filter((label) => !labelInside(label, scaled.width, scaled.height, margin) || !labelFits(label))
          .map((label) => label.id);
        expect(problems, `${site.id} at ${width}`).toEqual([]);
      }

      const narrow = scaleLayout(layout, 1248);
      const toolFont = narrow.labels.find((label) => label.kind === "tool")?.fontSize ?? 0;
      expect(toolFont).toBeGreaterThanOrEqual(11);
    }
  });
});

describe("site fixtures", () => {
  it("has the three plants and names every compared parameter", () => {
    expect(siteDetails.map((site) => site.id)).toEqual(["northwater", "ironwood", "lumen"]);
    expect(siteDetails[0]?.kpis.waferStartsPerHour).toBe(57.9);
    const etch = familyComparison("etch");
    expect(etch?.referenceSiteId).toBe("northwater");
    const flags = etch?.parameters.filter((parameter) => parameter.sites.some((site) => site.discrepancy)) ?? [];
    expect(flags.map((parameter) => parameter.parameter).sort()).toEqual(["escTemperature", "wallTemperature"]);
    expect(flags.find((parameter) => parameter.parameter === "escTemperature")?.sites.find((site) => site.siteId === "ironwood")?.reason).toMatch(/12 degrees hotter/);

    for (const family of ["deposition", "etch", "strip", "clean"]) {
      const comparison = familyComparison(family);
      expect(comparison?.parameters.length).toBeGreaterThan(0);
      for (const parameter of comparison?.parameters ?? []) {
        expect(isKnownParameter(parameter.parameter), parameter.parameter).toBe(true);
      }
    }
    expect(familyComparison("deposition")?.parameters.some((parameter) => parameter.sites.some((site) => site.discrepancy))).toBe(false);
  });
});
