// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company
import { describe, expect, it } from "vitest";
import { fleetSensors } from "virtual:fleet-sensors";
import { telemetryForTool } from "./telemetry";

describe("mock telemetry", () => {
  it("uses the fleet catalog sensor ids and units", async () => {
    const response = await fetch("/api/tools/E-02/telemetry");
    expect(response.status).toBe(200);
    const body = (await response.json()) as { sensorId: string; unit: string; value: number; status: string }[];
    const catalog = fleetSensors.filter((sensor) => sensor.toolId === "E-02");
    expect(body.map((row) => row.sensorId).sort()).toEqual(catalog.map((sensor) => sensor.id).sort());
    for (const row of body) {
      const sensor = catalog.find((item) => item.id === row.sensorId);
      expect(sensor).toBeDefined();
      expect(row.unit).toBe(sensor?.unit);
    }
    const esc = body.find((row) => row.sensorId === "E-02-A-TC-ESC");
    expect(esc).toMatchObject({ unit: "C", value: 53.1, status: "inSpec" });
    const reflected = body.find((row) => row.sensorId === "E-02-A-RF-SRC-RFL");
    expect(reflected?.unit).toBe("W");

    const deposition = await fetch("/api/tools/D-01/telemetry");
    const depositionBody = (await deposition.json()) as { sensorId: string; unit: string; value: number; status: string }[];
    expect(depositionBody.find((row) => row.sensorId === "D-01-A-PT-CHAMBER")).toMatchObject({
      unit: "Torr",
      value: 6.52,
      status: "outOfSpec",
    });

    const missing = await fetch("/api/tools/Z-99/telemetry");
    expect(missing.status).toBe(404);

    for (const toolId of ["D-01", "D-02", "E-01", "E-02", "S-01", "S-02", "C-01", "C-02"]) {
      const rows = telemetryForTool(toolId);
      const known = new Set(fleetSensors.filter((sensor) => sensor.toolId === toolId).map((sensor) => `${sensor.id}:${sensor.unit}`));
      expect(rows.map((row) => `${row.sensorId}:${row.unit}`).sort()).toEqual([...known].sort());
    }
  });
});
