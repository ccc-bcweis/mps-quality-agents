// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company
// One sensor list per tool for every dropdown (Tickets upload, Fault injector). Clones resolve through their template.
import { fleetSensors, fleetTemplates } from "virtual:fleet-sensors";
import { pointLabel, slotWord } from "./schematic";
import { sensorsForTool, type FleetSensor } from "./sensors";
import { parameterLabel } from "../labels";

export function sensorsFor(toolId: string): FleetSensor[] {
  return sensorsForTool(toolId, fleetSensors, fleetTemplates)
    .slice()
    .sort((left, right) => sensorOptionLabel(left).localeCompare(sensorOptionLabel(right)));
}

export function sensorOptionLabel(sensor: FleetSensor): string {
  const named = sensor.parameter ? parameterLabel(sensor.parameter) : pointLabel(sensor.schematicAnchor);
  const plain = !sensor.parameter || named === sensor.parameter ? pointLabel(sensor.schematicAnchor) : named;
  return `${slotWord(sensor.schematicAnchor)} · ${plain} (${sensor.unit}), spec ${String(sensor.limits.minimum)}-${String(sensor.limits.maximum)}`;
}

/** Parameter name the API takes for a sensor. */
export function sensorParameter(sensor: FleetSensor): string {
  return sensor.parameter ?? sensor.id;
}
