// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company
import { describe, expect, it } from "vitest";
import { ruleLabel } from "./labels";

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
  it("are plain English, never the raw camel-case value", async () => {
    const { STAGE_LABEL, INVESTIGATION_STAGE_LABEL, CAPA_STAGE_LABEL } = await import("./labels");
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
