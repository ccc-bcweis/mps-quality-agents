// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company
import { describe, expect, it } from "vitest";
import { siteForTool, toolInPlant } from "./selection";

describe("plant selection", () => {
  it("uses the tool site id, then the id prefix", () => {
    expect(siteForTool("D-01", "northwater")).toBe("northwater");
    expect(siteForTool("I-E-01")).toBe("ironwood");
    expect(siteForTool("P-C-01")).toBe("lumen");
    expect(siteForTool("E-02")).toBe("northwater");
    expect(siteForTool("I-E-01", "northwater")).toBe("northwater");
    expect(toolInPlant("all", "I-E-01")).toBe(true);
    expect(toolInPlant("ironwood", "I-E-01")).toBe(true);
    expect(toolInPlant("Ironwood", "E-02", "ironwood")).toBe(true);
    expect(toolInPlant("lumen", "D-01", "northwater")).toBe(false);
  });
});
