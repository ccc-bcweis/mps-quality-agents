// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company
import { act, renderHook, waitFor } from "@testing-library/react";
import { delay, http, HttpResponse } from "msw";
import { describe, expect, it } from "vitest";
import { fleetSensors, fleetTemplates } from "virtual:fleet-sensors";
import type { LiveSeries, ToolLive } from "../api/types";
import { sensorsForTool } from "../fleet/sensors";
import { server } from "../test/server";
import { emitHubEvent, setHubConnected } from "./fleetHub";
import { chartSeries, chartSeriesKey } from "./overlay";
import { applyToolDelta, type ToolLiveDelta } from "./stream";
import { useToolLive } from "./useLive";

// Live (Oct 17): on many sensors the plotted data redrew into a different-looking graph and back a few seconds later.
// A chart must always draw its own sensor's series, and streamed updates must never put another series' data in it.

const allToolIds = [...new Set([...fleetSensors.map((sensor) => sensor.toolId), ...Object.keys(fleetTemplates)])].sort();

function series(parameter: string, chamberId: string, values: number[], start = 0): LiveSeries {
  return {
    parameter,
    chamberId,
    points: values.map((value, index) => ({ at: new Date(Date.UTC(2026, 9, 17, 23, 0, start + index)).toISOString(), value })),
    center: null,
    sigma: null,
  };
}

function live(toolId: string, rows: LiveSeries[]): ToolLive {
  return { toolId, siteId: "northwater", simulatedNow: "2026-10-17T23:00:00Z", status: "nominal", readings: [], series: rows };
}

describe("every sensor on every tool charts its own series", () => {
  it("covers the whole fleet", () => {
    expect(allToolIds.length).toBeGreaterThan(20);
  });

  it.each(allToolIds)("%s: each sensor's series key is unique and resolves to that sensor's data", (toolId) => {
    const sensors = sensorsForTool(toolId, fleetSensors, fleetTemplates);
    expect(sensors.length).toBeGreaterThan(0);
    const keys = sensors.map((sensor) => {
      const key = chartSeriesKey(sensor);
      return `${key.parameter}|${key.chamberId ?? toolId}`;
    });
    expect(new Set(keys).size).toBe(keys.length);

    // The server streams one series per sensor, keyed by parameter (or sensor id) and chamber (or the tool).
    const rows = sensors.map((sensor, index) => {
      const key = chartSeriesKey(sensor);
      return series(key.parameter, key.chamberId ?? toolId, [index, index]);
    });
    const snapshot = live(toolId, rows);
    sensors.forEach((sensor, index) => {
      const key = chartSeriesKey(sensor);
      expect(chartSeries(snapshot, key.parameter, key.chamberId)?.points[0]?.value, sensor.id).toBe(index);
    });
  });

  it("never falls back to the other chamber when its own series is missing", () => {
    const snapshot = live("E-01", [series("chamberPressure", "E-01-B", [35, 35])]);
    expect(chartSeries(snapshot, "chamberPressure", "E-01-A")).toBeUndefined();
    expect(chartSeries(snapshot, "chamberPressure", "E-01-B")?.chamberId).toBe("E-01-B");
  });
});

describe("streamed updates", () => {
  const base = live("E-01", [series("chamberPressure", "E-01-A", [45, 45, 45]), series("chamberPressure", "E-01-B", [35, 35, 35])]);

  function delta(rows: LiveSeries[], toolId = "E-01"): ToolLiveDelta {
    return { toolId, siteId: "northwater", simulatedNow: "2026-10-17T23:00:10Z", status: "nominal", readings: [], series: rows, resync: false };
  }

  it("never replace one series' points with another series' data", () => {
    const next = applyToolDelta(base, delta([series("chamberPressure", "E-01-B", [36], 3), series("E-01-A-RF-BIAS-RFL", "E-01-A", [10], 3)]));
    expect(next).not.toBeNull();
    const a = chartSeries(next, "chamberPressure", "E-01-A");
    expect(a?.points.map((point) => point.value)).toEqual([45, 45, 45]);
    expect(chartSeries(next, "chamberPressure", "E-01-B")?.points.map((point) => point.value)).toEqual([35, 35, 35, 36]);
    expect(chartSeries(next, "E-01-A-RF-BIAS-RFL", "E-01-A")?.points.map((point) => point.value)).toEqual([10]);
  });

  it("drop duplicate and older points instead of reordering the buffer", () => {
    const next = applyToolDelta(base, delta([series("chamberPressure", "E-01-A", [99, 99, 46], 1)]));
    expect(chartSeries(next, "chamberPressure", "E-01-A")?.points.map((point) => point.value)).toEqual([45, 45, 45, 46]);
  });

  it("from another tool do not touch this tool", () => {
    expect(applyToolDelta(base, delta([series("chamberPressure", "E-01-A", [1], 3)], "E-02"))).toBeNull();
  });

  it("that arrive while the first load runs are applied on top of it, with one load and no older window afterwards", async () => {
    setHubConnected(true);
    let loads = 0;
    server.use(
      http.get("/api/tools/:toolId/live", async () => {
        loads += 1;
        await delay(150);
        return HttpResponse.json(live("E-01", [series("chamberPressure", "E-01-A", [45, 45, 45])]));
      }),
    );
    const { result } = renderHook(() => useToolLive("E-01"));
    act(() => {
      emitHubEvent("toolLive", delta([series("chamberPressure", "E-01-A", [46], 3)]));
      emitHubEvent("toolLive", delta([series("chamberPressure", "E-01-A", [47], 4)]));
    });
    await waitFor(() => expect(chartSeries(result.current, "chamberPressure", "E-01-A")?.points.length).toBe(5));
    expect(chartSeries(result.current, "chamberPressure", "E-01-A")?.points.map((point) => point.value)).toEqual([45, 45, 45, 46, 47]);
    await delay(300);
    expect(loads).toBe(1);
    expect(chartSeries(result.current, "chamberPressure", "E-01-A")?.points.length).toBe(5);
  });
});
