// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company
import { describe, expect, it } from "vitest";
import type { FleetLiveTool, ToolLive } from "../api/types";
import { recipeLabel, seriesPoints, sparkLine, toolTile } from "./overlay";

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
  it("shows no values before the first reading; status comes from the alert log", () => {
    expect(toolTile("D-02", [], undefined)).toEqual({ status: "nominal", params: [], spark: [] });
    expect(toolTile("D-02", [{ toolId: "D-02", status: "active" }], { ...liveTool, readings: [] })).toEqual({ status: "alarm", params: [], spark: [] });
    expect(toolTile("D-02", [{ toolId: "D-02", status: "cleared" }], undefined).status).toBe("nominal");
    expect(sparkLine(undefined)).toEqual([]);
    expect(sparkLine([9.5])).toEqual([9.5, 9.5]);
  });

  it("takes status, readings and the spark from the stream once it has a sample", () => {
    const next = toolTile("D-02", [], liveTool, [4, 9.5]);
    expect(next.status).toBe("alarm");
    expect(next.params[0]).toMatchObject({ label: "Chamber pressure", value: "9.50 Torr", tone: "alarm" });
    expect(next.spark).toEqual([4, 9.5]);
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

describe("recipeLabel", () => {
  it("shows the readable recipe name from the live data and a dash before it loads", () => {
    expect(recipeLabel({ recipeName: "PECVD oxide" })).toBe("PECVD oxide");
    expect(recipeLabel(undefined)).toBe("—");
    expect(recipeLabel(null)).toBe("—");
  });
});
