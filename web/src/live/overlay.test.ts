// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company
import { describe, expect, it } from "vitest";
import type { ToolStory } from "../fleet/catalog";
import type { FleetLiveTool, ToolLive } from "../api/types";
import { seriesPoints, sparkLine, storyWithLive } from "./overlay";

const story: ToolStory = {
  status: "nominal",
  recipe: "DEP-SIO-2",
  note: "In window.",
  lifecycle: "alert",
  escalated: false,
  params: [{ label: "Chamber P", value: "4.05 Torr" }],
  spark: [1, 2, 3],
  chart: { parameter: "chamberPressure", label: "Chamber pressure", unit: "Torr", mean: 4, sigma: 0.35, shape: "nominal" },
};

const liveTool: FleetLiveTool = {
  toolId: "D-02",
  siteId: "northwater",
  status: "alarm",
  readings: [
    {
      sensorId: "D-02-A-PT-CHAMBER",
      parameter: "chamberPressure",
      chamberId: "D-02-A",
      unit: "Torr",
      at: "2026-09-01T00:02:00Z",
      value: 9.5,
      status: "alarm",
    },
  ],
};

describe("live overlay", () => {
  it("leaves the teaching story in place when the live list is empty", () => {
    expect(storyWithLive(story, undefined)).toBe(story);
    expect(storyWithLive(story, { ...liveTool, readings: [] })).toBe(story);
    expect(sparkLine(undefined, story.spark)).toEqual([1, 2, 3]);
    expect(sparkLine([9.5], story.spark)).toEqual([9.5, 9.5]);
  });

  it("replaces status and the reading when the stream has a sample", () => {
    const next = storyWithLive(story, liveTool, [4, 9.5]);
    expect(next.status).toBe("alarm");
    expect(next.params[0]).toMatchObject({ label: "Chamber pressure", value: "9.50 Torr", tone: "alarm" });
    expect(next.spark).toEqual([4, 9.5]);
    expect(next.recipe).toBe(story.recipe);
  });

  it("plots only the series for the parameter on screen", () => {
    const live: ToolLive = {
      toolId: "E-01",
      siteId: "northwater",
      simulatedNow: "2026-09-01T00:02:00Z",
      status: "watch",
      readings: [],
      series: [
        { parameter: "etchRate", chamberId: "E-01-B", points: [{ at: "2026-09-01T00:00:00Z", value: 1 }] },
        {
          parameter: "etchRate",
          chamberId: "E-01-A",
          points: [
            { at: "2026-09-01T00:00:00Z", value: 350 },
            { at: "2026-09-01T00:01:00Z", value: 250 },
          ],
        },
      ],
    };
    expect(seriesPoints(live, "etchRate", "E-01-A")).toEqual([
      { index: 0, value: 350 },
      { index: 1, value: 250 },
    ]);
    expect(seriesPoints(live, "escTemperature")).toBeUndefined();
    expect(seriesPoints(null, "etchRate")).toBeUndefined();
  });
});
