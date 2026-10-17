// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company
import { describe, expect, it } from "vitest";
import { capaStageLabel, CAPA_STAGES, CATEGORIES, CATEGORY_LABEL, INVESTIGATION_STAGE_LABEL, limits, parameterLabel, plants, ruleBrief, ruleLabel, setCatalog, siteForTool, STAGE_LABEL } from ".";
import fixture from "../test/catalog.fixture.json";

describe("ruleLabel for an alert that kept firing", () => {
  it("collapses joined rule ids into plain words", () => {
    expect(ruleLabel("western-electric-1+western-electric-2+western-electric-3+western-electric-4+nelson-6-rising")).toBe(
      "Western Electric rules 1-4 and Nelson rule 6 (rising trend)",
    );
    expect(ruleLabel("western-electric-1+western-electric-3")).toBe("Western Electric rules 1 and 3");
    expect(ruleLabel("western-electric-1+western-electric-1")).toMatch(/^Western Electric rules, rule 1:/);
    expect(ruleLabel("western-electric-1")).toMatch(/^Western Electric rules, rule 1:/);
  });
});

describe("stage labels", () => {
  it("are plain English, never the raw camel-case value", () => {
    const CAPA_STAGE_LABEL = Object.fromEntries(CAPA_STAGES.map((stage) => [stage.id, stage.label]));
    for (const labels of [STAGE_LABEL, INVESTIGATION_STAGE_LABEL, CAPA_STAGE_LABEL]) {
      for (const [key, label] of Object.entries(labels)) {
        expect(label, key).not.toMatch(/[a-z][A-Z]/);
        expect(label, key).toMatch(/^[A-Z]/);
      }
    }
    expect(STAGE_LABEL.recommendationReady).toBe("Recommendation ready");
    expect(INVESTIGATION_STAGE_LABEL.recommendationReady).toBe("Recommendation ready");
  });
});

describe("the server catalog", () => {
  it("gives every rule sentence, number, parameter name and plant", () => {
    expect(ruleLabel("western-electric-2")).toBe(fixture.rules.find((rule) => rule.id === "western-electric-2")?.label);
    expect(ruleBrief("western-electric-4")).toBe(fixture.rules.find((rule) => rule.id === "western-electric-4")?.brief);
    expect(limits()?.clearReadings).toBe(fixture.limits.clearReadings);
    expect(parameterLabel("cf4Flow")).toBe("CF4 flow");
    expect(plants().map((plant) => plant.id)).toEqual(fixture.sites.map((site) => site.id));
  });

  it("follows the catalog when the data changes, with no copy in the console", () => {
    setCatalog({
      ...fixture,
      rules: fixture.rules.map((rule) => (rule.id === "western-electric-1" ? { ...rule, label: "Western Electric rules, rule 1: one point is more than 2.5σ from the average" } : rule)),
      parameters: [{ id: "cf4Flow", label: "Etch gas" }],
      sites: [
        { id: "home", name: "Home Fab", toolPrefix: "" },
        { id: "west", name: "West Fab", toolPrefix: "W-" },
      ],
    });
    expect(ruleLabel("western-electric-1")).toMatch(/2\.5σ/);
    expect(parameterLabel("cf4Flow")).toBe("Etch gas");
    expect(parameterLabel("escTemperature")).toBe("Esc Temperature");
    expect(siteForTool("W-E-01")).toBe("west");
    expect(siteForTool("E-01")).toBe("home");
    expect(siteForTool("E-01", "Lumen")).toBe("lumen");
  });

  it("keeps one category list and one CAPA stage list", () => {
    expect(CATEGORIES.map((category) => category.label)).toEqual(Object.values(CATEGORY_LABEL));
    expect(capaStageLabel("rootCauseConfirmed")).toBe("Root cause confirmed");
  });
});
