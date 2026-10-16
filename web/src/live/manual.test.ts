// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company
import { describe, expect, it } from "vitest";
import type { Alert, LiveSeries, ManualReading } from "../api/types";
import { isOlderThanWindow, latestManual, manualMarker, manualTile } from "./manual";
import { statusReasons } from "./statusReasons";

const reading: ManualReading = {
  evidenceId: "e1",
  incidentId: "i1",
  alertId: "a1",
  toolId: "E-01",
  siteId: "northwater",
  chamberId: "E-01-A",
  parameter: "escTemperature",
  instrument: "thermocoupleLogger",
  value: 68,
  unit: "C",
  specMinimum: 40,
  specMaximum: 60,
  outOfSpec: true,
  observedAt: "2026-10-12T09:40:00Z",
  olderThanWindow: true,
  source: "Manual reading from the thermocouple logger printout thermocouple-logger-e01.png, operator JL: 68 C (spec 40-60), out of spec.",
};

const series: LiveSeries = {
  parameter: "escTemperature",
  chamberId: "E-01-A",
  points: [
    { at: "2026-10-15T23:00:00Z", value: 50 },
    { at: "2026-10-15T23:00:15Z", value: 51 },
    { at: "2026-10-15T23:00:30Z", value: 50 },
  ],
};

describe("manual readings", () => {
  it("pins a reading older than the window to the left edge and flags it", () => {
    const marker = manualMarker(series, reading)!;
    expect(marker.index).toBe(0);
    expect(marker.clamped).toBe(true);
    expect(marker.value).toBe(68);
    expect(marker.label).toContain("thermocouple logger");
    expect(isOlderThanWindow(reading.observedAt, series.points[0].at)).toBe(true);
  });

  it("places a Now-corrected reading in the window", () => {
    const marker = manualMarker(series, { ...reading, observedAt: "2026-10-15T23:00:20Z", olderThanWindow: false })!;
    expect(marker.index).toBe(1);
    expect(marker.clamped).toBe(false);
    expect(isOlderThanWindow("2026-10-15T23:00:20Z", series.points[0].at)).toBe(false);
  });

  it("shows the printed value on the tile with a Manual badge, colored out of spec", () => {
    const tile = manualTile(reading);
    expect(tile.value).toBe(68);
    expect(tile.status).toBe("outOfSpec");
    expect(tile.manual.label).toBe("Manual");
    expect(manualTile({ ...reading, value: 50, outOfSpec: false }).status).toBe("inSpec");
  });

  it("matches the chamber", () => {
    expect(latestManual([reading], "escTemperature", "E-01-B")).toBeUndefined();
    expect(latestManual([reading], "escTemperature", "E-01-A")).toBe(reading);
  });

  it("explains a status with the printout and instrument behind the alert", () => {
    const alert = { id: "a1", toolId: "E-01", chamberId: "E-01-A", parameter: "escTemperature", severity: "high", rule: "manualMeasurement", status: "active" } as Alert;
    const reasons = statusReasons("alarm", [alert], [reading]);
    expect(reasons).toHaveLength(1);
    expect(reasons[0].text).toMatch(/^Alarm: Manual reading from the thermocouple logger printout/);
    expect(statusReasons("nominal", [alert], [reading])).toEqual([]);
    expect(statusReasons("alarm", [{ ...alert, status: "cleared" }], [reading])).toEqual([]);
  });
});
