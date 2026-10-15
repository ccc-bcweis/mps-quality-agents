// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company
import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";
import { describe, expect, it } from "vitest";
import { fleetSensorNote, fleetSensorSource, fleetSensors, fleetTemplates } from "virtual:fleet-sensors";
import { parseCloneTemplates, parseFleetSensors, sensorsForTool, statusFromLimits, toolIdsInFleetYaml } from "./sensors";

const repoRoot = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "../../..");
const webRoot = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "../..");

const sample = `
family: etch
tools:
  - id: E-01
    name: Etch E-01
    sensors:
      - { id: E-01-A-PT, type: chamberPressure, unit: mTorr, subsystem: chamber, schematicAnchor: a.chamber.pressure, minimum: 20, maximum: 80, nominal: 45, warningMinimum: 26, warningMaximum: 74, chamberId: E-01-A, parameter: chamberPressure }
  - id: E-02
    name: Etch E-02
    sensors:
      - id: E-02-A-TC-ESC
        type: thermocouple
        unit: C
        subsystem: chuckHeater
        schematicAnchor: a.chuck.esc
        minimum: 40
        maximum: 60
        nominal: 50
        warningMinimum: 42
        warningMaximum: 58
        chamberId: E-02-A
        parameter: escTemperature
`;

describe("fleet sensor parser", () => {
  it("reads the sensors nested under each tool", () => {
    const parsed = parseFleetSensors(sample);
    expect(parsed.map((sensor) => `${sensor.toolId}:${sensor.id}`)).toEqual(["E-01:E-01-A-PT", "E-02:E-02-A-TC-ESC"]);
    expect(parsed[1]).toMatchObject({
      family: "etch",
      unit: "C",
      schematicAnchor: "a.chuck.esc",
      limits: { minimum: 40, maximum: 60, nominal: 50, warningMinimum: 42, warningMaximum: 58 },
    });
    expect(toolIdsInFleetYaml(sample)).toEqual(["E-01", "E-02"]);
  });

  it("rejects a sensor that is missing an anchor, a limit, or a warning band", () => {
    expect(() =>
      parseFleetSensors(
        "family: etch\ntools:\n  - id: E-02\n    sensors:\n      - { id: only, type: t, unit: u, subsystem: chamber, minimum: 0, maximum: 1, nominal: 0, warningMinimum: 0, warningMaximum: 1 }\n",
      ),
    ).toThrow(/schematicAnchor/);
    expect(() =>
      parseFleetSensors(
        "family: etch\ntools:\n  - id: E-02\n    sensors:\n      - { id: bad, type: t, unit: u, subsystem: chamber, schematicAnchor: a.chamber.pressure, maximum: 1, nominal: 0, warningMinimum: 0, warningMaximum: 1 }\n",
      ),
    ).toThrow(/minimum/);
    expect(() =>
      parseFleetSensors(
        "family: etch\ntools:\n  - id: E-02\n    sensors:\n      - { id: bad, type: t, unit: u, subsystem: chamber, schematicAnchor: a.chamber.pressure, minimum: 0, maximum: 1, nominal: 0, warningMinimum: 0, warningMaximum: 2 }\n",
      ),
    ).toThrow(/warning band/);
  });

  it("loads data/fleet at build time and does not keep a fallback catalog", () => {
    expect(fs.existsSync(path.join(webRoot, "src/fleet/sensors.fallback.json"))).toBe(false);
    expect(fleetSensorSource).toBe("data/fleet");
    expect(fleetSensorNote).toBe("");
    const etch = fs.readFileSync(path.join(repoRoot, "data/fleet/mps-e7.yaml"), "utf8");
    const parsed = parseFleetSensors(etch);
    const esc = parsed.find((sensor) => sensor.id === "E-02-A-TC-ESC");
    expect(esc).toMatchObject({
      toolId: "E-02",
      family: "etch",
      type: "thermocouple",
      unit: "C",
      subsystem: "chuckHeater",
      schematicAnchor: "a.chuck.esc",
      chamberId: "E-02-A",
      parameter: "escTemperature",
      limits: { minimum: 40, maximum: 60, nominal: 50, warningMinimum: 42, warningMaximum: 58 },
    });
    expect(parsed.find((sensor) => sensor.id === "E-02-A-RF-SRC-RFL")?.tracksSensorId).toBe("E-02-A-RF-SRC-FWD");
    expect(fleetSensors.filter((sensor) => sensor.toolId === "E-02").length).toBeGreaterThan(20);
    expect(fleetSensors.some((sensor) => sensor.family === "clean" && sensor.type === "rfForwardPower")).toBe(false);
    expect(fleetSensors.some((sensor) => sensor.family === "clean" && sensor.type === "megasonicPower")).toBe(true);
  });
});

describe("clone sensor ids", () => {
  it("rewrites a placed tool onto its template", () => {
    const templates = parseCloneTemplates(`
      - { id: D-01 }
      - { id: D-03, template: D-01, name: Deposition D-03 }
      - { id: I-E-02, template: E-01, name: Etch I-E-02 }
    `);
    expect(templates["D-01"]).toBeUndefined();
    expect(templates["D-03"]).toBe("D-01");
    expect(fleetTemplates["D-03"]).toBe("D-01");
    expect(fleetTemplates["I-D-01"]).toBe("D-01");
    expect(fleetTemplates["I-E-02"]).toBe("E-01");
    expect(fleetTemplates["E-03"]).toBe("E-01");

    const deposition = sensorsForTool("D-03", fleetSensors, fleetTemplates);
    expect(deposition.length).toBe(fleetSensors.filter((sensor) => sensor.toolId === "D-01").length);
    expect(deposition.every((sensor) => sensor.toolId === "D-03")).toBe(true);
    expect(deposition.some((sensor) => sensor.id === "D-03-A-PT-CHAMBER")).toBe(true);
    expect(deposition.some((sensor) => sensor.id.startsWith("D-01"))).toBe(false);

    const etch = sensorsForTool("I-E-02", fleetSensors, fleetTemplates);
    expect(etch.some((sensor) => sensor.id === "I-E-02-A-PT-CHAMBER")).toBe(true);
    expect(etch.some((sensor) => sensor.chamberId === "I-E-02-A")).toBe(true);
    expect(sensorsForTool("E-02", fleetSensors, fleetTemplates).some((sensor) => sensor.id === "E-02-A-TC-ESC")).toBe(true);
  });
});

describe("spec limit status", () => {
  const limits = { minimum: 40, maximum: 60, nominal: 50, warningMinimum: 42, warningMaximum: 58 };

  it("uses the warning band inside the spec and the spec as the outer limit", () => {
    expect(statusFromLimits(50, limits)).toBe("inSpec");
    expect(statusFromLimits(42, limits)).toBe("inSpec");
    expect(statusFromLimits(58, limits)).toBe("inSpec");
    expect(statusFromLimits(41.9, limits)).toBe("warning");
    expect(statusFromLimits(58.1, limits)).toBe("warning");
    expect(statusFromLimits(40, limits)).toBe("warning");
    expect(statusFromLimits(39.9, limits)).toBe("outOfSpec");
    expect(statusFromLimits(60.1, limits)).toBe("outOfSpec");
  });
});
