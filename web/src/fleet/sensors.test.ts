// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company
import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";
import { describe, expect, it } from "vitest";
import { fleetSensorNote, fleetSensorSource, fleetSensors } from "virtual:fleet-sensors";
import { parseFleetSensors, toolIdsInFleetYaml } from "./sensors";

const repoRoot = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "../../..");

const sample = `
family: etch
tools:
  - id: E-01
    name: Etch E-01
  - id: E-02
    name: Etch E-02
sensors:
  - id: shared-pressure
    type: chamberPressure
    unit: mTorr
    subsystem: vacuum
    schematicAnchor: vacuum.pressure
    label: Chamber pressure
  - { id: e02-esc, toolId: E-02, type: escTemperature, unit: "°C", subsystem: chuck, schematicAnchor: chuck.temperature, label: "ESC temperature", value: 53.1, display: "53.1 °C osc", status: warning }
`;

describe("fleet sensor parser", () => {
  it("reads a root sensors list and leaves the current fleet files untouched", () => {
    const etch = fs.readFileSync(path.join(repoRoot, "data/fleet/mps-e7.yaml"), "utf8");
    expect(parseFleetSensors(etch)).toEqual([]);
    expect(toolIdsInFleetYaml(etch)).toEqual(["E-01", "E-02"]);
    const parsed = parseFleetSensors(sample);
    expect(parsed.map((sensor) => `${sensor.toolId}:${sensor.id}`)).toEqual([
      "E-01:shared-pressure:E-01",
      "E-02:shared-pressure:E-02",
      "E-02:e02-esc",
    ]);
    expect(parsed[2]).toMatchObject({ value: 53.1, status: "warning", schematicAnchor: "chuck.temperature" });
  });

  it("rejects a sensor that is missing an anchor or has an unknown status", () => {
    expect(() =>
      parseFleetSensors("sensors:\n  - { id: only, toolId: E-02, type: t, unit: u, subsystem: chamber }\n"),
    ).toThrow(/schematicAnchor/);
    expect(() =>
      parseFleetSensors(
        "sensors:\n  - { id: bad, toolId: E-02, type: t, unit: u, subsystem: chamber, schematicAnchor: chamber.body, status: bad }\n",
      ),
    ).toThrow(/unknown status/);
  });

  it("loads data/fleet sensors at build time, or the stub when none are declared", () => {
    const dir = path.join(repoRoot, "data/fleet");
    const declared = fs.existsSync(dir)
      && fs.readdirSync(dir).some((name) => /^sensors:/m.test(fs.readFileSync(path.join(dir, name), "utf8")));
    if (declared) {
      expect(fleetSensorSource).toBe("data/fleet");
    } else {
      expect(fleetSensorSource).toBe("web/src/fleet/sensors.fallback.json");
      expect(fleetSensorNote.length).toBeGreaterThan(0);
    }
    expect(fleetSensors.length).toBeGreaterThan(0);
    expect(fleetSensors.some((sensor) => sensor.toolId === "E-02" && sensor.schematicAnchor === "chuck.temperature")).toBe(true);
    expect(fleetSensors.some((sensor) => sensor.toolId === "D-01" && sensor.status === "outOfSpec")).toBe(true);
  });
});
