// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company
import type { FleetLiveTool, LiveReading, LiveSeries, ToolLive } from "../api/types";
import type { Tone } from "../api/labels";
import { statusFromAlerts, type ToolTile, type WindowRow } from "../fleet/catalog";
import { formatValue, type SensorStatus } from "../fleet/telemetry";
import type { SpcPoint } from "../spc/series";
import { LIMIT_LABEL, limitTone, rangeLabel } from "../investigation/limits";
import { PARAMETER_LABEL, unitLabel } from "../sites/parameters";

// Keys are the lower-cased parts of a sensor id.
const SENSOR_WORDS: Record<string, string> = {
  rf: "RF",
  bias: "Bias",
  src: "Source",
  rfl: "reflected",
  fwd: "forward",
  output: "Output",
  throughput: "Throughput",
  run: "Run",
  rate: "rate",
  temp: "temperature",
  press: "pressure",
};

/**
 * A plain label for a sensor id such as E-01-A-RF-BIAS-RFL: "A Bias RF reflected".
 * The tool id is dropped (the tile already names the tool) and the chamber letter leads.
 */
export function sensorLabel(sensorId: string): string | null {
  const match = /^(?:[A-Z]-)?[A-Z]-\d{2}(?:-([A-Z]))?-(.+)$/.exec(sensorId);
  if (!match) return null;
  const [, chamber, rest] = match;
  const tokens = rest.toLowerCase().split("-");
  const word = (token: string) => SENSOR_WORDS[token] ?? token;
  const words =
    tokens[0] === "rf" && (tokens[1] === "bias" || tokens[1] === "src")
      ? [word(tokens[1]), "RF", ...tokens.slice(2).map(word)]
      : tokens.map(word);
  const text = words.join(" ");
  const sentence = text.charAt(0).toUpperCase() + text.slice(1);
  return chamber ? `${chamber} ${sentence}` : sentence;
}

export function parameterLabel(parameter: string): string {
  const known = PARAMETER_LABEL[parameter];
  if (known) return known;
  const sensor = sensorLabel(parameter);
  if (sensor) return sensor;
  const words = parameter.replace(/([A-Z])/g, " $1").trim();
  return words.charAt(0).toUpperCase() + words.slice(1);
}

function toneFor(status: LiveReading["status"]): Tone | undefined {
  if (status === "alarm") return "alarm";
  if (status === "watch") return "warn";
  return undefined;
}

/** A spark needs two points. One live sample is drawn as a flat line; none draws nothing. */
export function sparkLine(values: number[] | undefined): number[] {
  if (!values || values.length === 0) return [];
  if (values.length === 1) return [values[0], values[0]];
  return values;
}

/**
 * The tile for a tool from live data only: status, the first three readings and the spark. Before the first reading
 * the status comes from the alert log and the tile has no values (the page shows "Waiting for readings…").
 */
export function toolTile(toolId: string, alerts: { toolId: string; status?: string }[], live: FleetLiveTool | undefined, spark?: number[]): ToolTile {
  if (!live || live.readings.length === 0) return { status: statusFromAlerts(toolId, alerts), params: [], spark: [] };
  const params = live.readings.slice(0, 3).map((reading) => ({
    label: parameterLabel(reading.parameter),
    value: `${formatValue(reading.value, reading.unit)} ${unitLabel(reading.unit)}`,
    tone: toneFor(reading.status),
  }));
  return { status: live.status, params, spark: sparkLine(spark) };
}

/**
 * Operating windows from data only: each live reading with a recipe-step window becomes a row. Spec, control limits,
 * colour and wording all come from the server's window and limit state. No reading, no row.
 */
export function overlayWindows(readings: LiveReading[] | undefined): WindowRow[] {
  if (!readings) return [];
  return readings
    .filter((reading) => reading.specLow != null && reading.specHigh != null)
    .map((reading) => ({
      parameter: `${reading.parameter}|${reading.chamberId}`,
      label: `${parameterLabel(reading.parameter)} (${reading.chamberId})`,
      window: rangeLabel(reading.specLow, reading.specHigh, reading.unit),
      control: rangeLabel(reading.controlLow, reading.controlHigh, reading.unit),
      now: `${formatValue(reading.value, reading.unit)} ${unitLabel(reading.unit)}`,
      tone: limitTone(reading.limitState),
      state: reading.limitState ? LIMIT_LABEL[reading.limitState] : "—",
    }));
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

/** The running recipe's readable name from the fleet data; a dash until the first live load. */
export function recipeLabel(live: { recipeName?: string | null } | null | undefined): string {
  return live?.recipeName ?? "—";
}
