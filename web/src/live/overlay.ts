// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company
import type { FleetLiveTool, LiveReading, LiveSeries, ToolLive } from "../api/types";
import type { Tone } from "../api/labels";
import type { ToolStory, WindowRow } from "../fleet/catalog";
import { formatValue, type SensorStatus } from "../fleet/telemetry";
import type { SpcPoint } from "../spc/series";

const PARAMETER_LABEL: Record<string, string> = {
  chamberPressure: "Chamber pressure",
  escTemperature: "Chuck temperature",
  filmThickness: "Film thickness",
  vibrationRms: "Pump vibration",
  rfBiasPower: "Bias power",
  rfPower: "Power",
  etchRate: "Etch rate",
  cf4Flow: "Gas flow",
  heaterTemperature: "Heater",
  temperature: "Temperature",
  bathTemperature: "Bath",
  sourceRf: "Source power",
  biasRf: "Bias power",
  particles: "Particles",
};

export function parameterLabel(parameter: string): string {
  const known = PARAMETER_LABEL[parameter];
  if (known) return known;
  const words = parameter.replace(/([A-Z])/g, " $1").trim();
  return words.charAt(0).toUpperCase() + words.slice(1);
}

function toneFor(status: LiveReading["status"]): Tone | undefined {
  if (status === "alarm") return "alarm";
  if (status === "watch") return "warn";
  return undefined;
}

/** A spark needs two points. One live sample is drawn as a flat line. */
export function sparkLine(values: number[] | undefined, fallback: number[]): number[] {
  if (!values || values.length === 0) return fallback;
  if (values.length === 1) return [values[0], values[0]];
  return values;
}

/**
 * Live readings replace the frozen tile only when this tool has samples.
 * An empty fleet snapshot leaves the teaching-floor story in place.
 */
export function storyWithLive(story: ToolStory, live: FleetLiveTool | undefined, spark?: number[]): ToolStory {
  if (!live || live.readings.length === 0) return story;
  const params = live.readings.slice(0, 3).map((reading) => ({
    label: parameterLabel(reading.parameter),
    value: `${formatValue(reading.value, reading.unit)} ${reading.unit}`,
    tone: toneFor(reading.status),
  }));
  return {
    ...story,
    status: live.status,
    params,
    spark: sparkLine(spark, story.spark),
  };
}

export function overlayWindows(rows: WindowRow[], readings: LiveReading[] | undefined): WindowRow[] {
  if (!readings || readings.length === 0) return rows;
  return rows.map((row) => {
    const reading = readings.find((item) => item.parameter === row.parameter);
    if (!reading) return row;
    const tone: WindowRow["tone"] = reading.status === "alarm" ? "alarm" : reading.status === "watch" ? "warn" : "nominal";
    return {
      ...row,
      now: `${formatValue(reading.value, reading.unit)} ${reading.unit}`,
      tone,
    };
  });
}

export function sensorStatus(status: LiveReading["status"]): SensorStatus {
  if (status === "alarm") return "outOfSpec";
  if (status === "watch") return "warning";
  return "inSpec";
}

export function liveReadingMap(readings: LiveReading[] | undefined): Map<string, { value: number; unit: string; status: SensorStatus }> | undefined {
  if (!readings || readings.length === 0) return undefined;
  const map = new Map<string, { value: number; unit: string; status: SensorStatus }>();
  for (const reading of readings) {
    map.set(reading.sensorId, { value: reading.value, unit: reading.unit, status: sensorStatus(reading.status) });
  }
  return map;
}

export function seriesPoints(live: ToolLive | null, parameter: string, chamberId?: string): SpcPoint[] | undefined {
  if (!live) return undefined;
  const rows = live.series.filter((series) => series.parameter === parameter && series.points.length > 0);
  const match: LiveSeries | undefined =
    rows.find((series) => chamberId !== undefined && series.chamberId === chamberId) ?? rows[0];
  if (!match) return undefined;
  return match.points.map((point, index) => ({ index, value: point.value }));
}

/** The center and sigma the rules engine uses for this series, when the server sent them. */
export function seriesLimits(live: ToolLive | null, parameter: string, chamberId?: string): { mean: number; sigma: number } | undefined {
  if (!live) return undefined;
  const rows = live.series.filter((series) => series.parameter === parameter && series.points.length > 0);
  const match: LiveSeries | undefined =
    rows.find((series) => chamberId !== undefined && series.chamberId === chamberId) ?? rows[0];
  if (!match || match.center == null || match.sigma == null || !(match.sigma > 0)) return undefined;
  return { mean: match.center, sigma: match.sigma };
}
