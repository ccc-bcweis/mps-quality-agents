// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company
import { describe, expect, it } from "vitest";
import { labelFits, labelInside, layoutFlow, overlappingPairs, scaleFlow, type FlowStep } from "./flowLayout";

const WIDTHS = [1280, 1600, 1920];

const current: FlowStep[] = [
  { id: "notice", name: "Someone notices the excursion" },
  { id: "search", name: "Search each system" },
  { id: "email", name: "Hand off by email" },
  { id: "draft", name: "Draft a cause" },
  { id: "ask", name: "Ask a manager" },
  { id: "write", name: "Write the corrective action" },
];

const future: FlowStep[] = [
  { id: "rule", name: "A rule opens the ticket" },
  { id: "investigate", name: "The investigation gathers evidence" },
  { id: "rank", name: "A ranked cause is ready" },
  { id: "person", name: "A person decides", detail: "An escalated case cannot skip this step." },
  { id: "action", name: "The corrective action opens" },
];

describe("process map labels", () => {
  it.each([
    ["current", current],
    ["future", future],
  ])("keeps %s step labels inside the map and clear of each other at 1280, 1600, and 1920", (_name, steps) => {
    const layout = layoutFlow(steps);
    expect(layout.boxes).toHaveLength(steps.length);
    for (const width of WIDTHS) {
      const scaled = scaleFlow(layout, width);
      const margin = 8 * (width / layout.width);
      expect(overlappingPairs(scaled.boxes), `${width}`).toEqual([]);
      const problems = scaled.boxes
        .filter((box) => !labelInside(box, scaled.width, scaled.height, margin) || !labelFits(box))
        .map((box) => box.id);
      expect(problems, `${width}`).toEqual([]);
    }
  });
});
