// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company
import type { ChartSpec, ToolStory } from "./catalog";
import type { FleetSensor } from "./sensors";

/** Keep the tool's authored chart when the sensor is that same parameter. */
export function chartSpec(sensor: FleetSensor, story: ToolStory): ChartSpec {
  if (sensor.type === story.chart.parameter) return story.chart;
  const value = sensor.value ?? 0;
  const sigma = Math.max(Math.abs(value) * 0.05, 0.2);
  const shape = sensor.status === "outOfSpec" ? "step" : sensor.status === "warning" ? "oscillation" : "nominal";
  return {
    parameter: sensor.type,
    label: sensor.label,
    unit: sensor.unit,
    mean: value,
    sigma,
    shape,
    endValue: sensor.value,
  };
}
