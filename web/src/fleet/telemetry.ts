// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company
import { fleetSensors } from "virtual:fleet-sensors";
import type { FleetSensor } from "./sensors";
import { statusFromLimits, type SensorLimits } from "./sensors";

export type SensorStatus = "inSpec" | "warning" | "outOfSpec";

export type TelemetrySample = {
  sensorId: string;
  toolId: string;
  chamberId: string | null;
  parameter: string | null;
  unit: string;
  value: number;
  status: SensorStatus;
  simulatedAt: string;
};

/** One frozen demo sample. The same ids and units the simulator emits. */
export const TELEMETRY_AT = "2026-10-14T15:00:00.000Z";

/** Pitch scenes. D-01 chamber A pressure is outside the 2–6 Torr spec. E-02 chamber A ESC stays inside the warning band. */
const DEMO_VALUE: Record<string, number> = {
  "D-01-A-PT-CHAMBER": 6.52,
  "E-02-A-TC-ESC": 53.1,
};

function hashUnit(id: string): number {
  let hash = 2166136261;
  for (let index = 0; index < id.length; index += 1) {
    hash ^= id.charCodeAt(index);
    hash = Math.imul(hash, 16777619);
  }
  return (hash >>> 0) / 4294967296;
}

function decimals(unit: string): number {
  switch (unit) {
    case "atm":
    case "mm":
      return 3;
    case "Torr":
    case "mm/s":
    case "L/min":
      return 2;
    case "C":
    case "mTorr":
    case "percent":
    case "wafers/hour":
    case "s":
    case "sccm":
      return 1;
    default:
      return 0;
  }
}

export function formatValue(value: number, unit: string): string {
  return value.toFixed(decimals(unit));
}

function clamp(value: number, limits: SensorLimits): number {
  return Math.min(limits.warningMaximum, Math.max(limits.warningMinimum, value));
}

function unclamped(sensor: FleetSensor, byId: Map<string, FleetSensor>, stack: Set<string>, applyDemoFault: boolean): number {
  if (applyDemoFault) {
    const demo = DEMO_VALUE[sensor.id];
    if (demo !== undefined) return demo;
  }
  if (sensor.tracksSensorId && !stack.has(sensor.id)) {
    const forward = byId.get(sensor.tracksSensorId);
    if (forward && forward.limits.nominal !== 0) {
      stack.add(sensor.id);
      const forwardValue = unclamped(forward, byId, stack, applyDemoFault);
      return forwardValue * (sensor.limits.nominal / forward.limits.nominal);
    }
  }
  const span = sensor.limits.warningMaximum - sensor.limits.warningMinimum;
  if (span <= 0) return sensor.limits.nominal;
  return sensor.limits.warningMinimum + span * (0.35 + hashUnit(sensor.id) * 0.3);
}

function sample(sensor: FleetSensor, byId: Map<string, FleetSensor>, applyDemoFault: boolean): TelemetrySample {
  const raw = unclamped(sensor, byId, new Set(), applyDemoFault);
  const rounded = Number(formatValue(raw, sensor.unit));
  const value = applyDemoFault && DEMO_VALUE[sensor.id] !== undefined ? rounded : clamp(rounded, sensor.limits);
  return {
    sensorId: sensor.id,
    toolId: sensor.toolId,
    chamberId: sensor.chamberId ?? null,
    parameter: sensor.parameter ?? null,
    unit: sensor.unit,
    value,
    status: statusFromLimits(value, sensor.limits),
    simulatedAt: TELEMETRY_AT,
  };
}

export function telemetryFor(sensors: FleetSensor[], options?: { applyDemoFault?: boolean }): TelemetrySample[] {
  const applyDemoFault = options?.applyDemoFault === true;
  const byId = new Map(sensors.map((sensor) => [sensor.id, sensor]));
  return sensors.map((sensor) => sample(sensor, byId, applyDemoFault));
}

export function telemetryForTool(toolId: string, options?: { applyDemoFault?: boolean }): TelemetrySample[] {
  return telemetryFor(
    fleetSensors.filter((sensor) => sensor.toolId === toolId),
    options,
  );
}

export function readingFor(sensor: FleetSensor): TelemetrySample {
  const row = telemetryForTool(sensor.toolId).find((item) => item.sensorId === sensor.id);
  if (row) return row;
  // A clone sensor is a rewritten copy. Sample it on its own limits when the catalog has no row.
  return sample(sensor, new Map([[sensor.id, sensor]]), false);
}
