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
