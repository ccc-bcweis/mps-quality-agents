// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company
import type { ChartSpec, ToolStory } from "./catalog";
import type { FleetSensor } from "./sensors";
import { sensorLabel } from "./schematic";
import { readingFor } from "./telemetry";

/** Chamber A keeps the tool's authored chart when the sensor measures that parameter. */
export function chartSpec(sensor: FleetSensor, story: ToolStory): ChartSpec {
  const reading = readingFor(sensor);
  const label = sensorLabel(sensor.schematicAnchor);
  if (sensor.parameter && sensor.parameter === story.chart.parameter && sensor.chamberId?.endsWith("-A")) {
    return { ...story.chart, label, unit: sensor.unit };
  }
  const span = sensor.limits.maximum - sensor.limits.minimum;
  const sigma = Math.max(span / 6, 0.2);
  const shape = reading.status === "outOfSpec" ? "step" : reading.status === "warning" ? "oscillation" : "nominal";
  return {
    parameter: sensor.parameter ?? sensor.id,
    label,
    unit: sensor.unit,
    mean: sensor.limits.nominal,
    sigma,
    shape,
    endValue: reading.value,
  };
}
