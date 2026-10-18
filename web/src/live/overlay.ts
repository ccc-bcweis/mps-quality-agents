// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company
import type { FleetLiveTool, LiveReading, LiveSeries, ToolLive } from "../api/types";
import type { Tone } from "../api/labels";
import { statusFromAlerts, type ToolTile, type WindowRow } from "../fleet/catalog";
import { formatValue, type SensorStatus } from "../fleet/telemetry";
import type { SpcPoint, StepLimit } from "../spc/series";
import { LIMIT_LABEL, limitTone, rangeLabel } from "../investigation/limits";
import { unitLabel } from "../sites/parameters";
import { parameterLabel } from "../labels";

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

/**
 * The one series a chart draws for a sensor. A sensor with a chamber matches that chamber only: never the other
 * chamber's series with the same parameter, which made the chart swap to a different-looking graph and back whenever
 * its own series was briefly missing. A tool-level sensor (no chamber) matches the tool's own series.
 */
export function chartSeries(live: ToolLive | null, parameter: string, chamberId?: string | null): LiveSeries | undefined {
  if (!live) return undefined;
  const rows = live.series.filter((series) => series.parameter === parameter && series.points.length > 0);
  if (chamberId) return rows.find((series) => series.chamberId === chamberId);
  return rows.find((series) => series.chamberId === live.toolId) ?? (rows.length === 1 ? rows[0] : undefined);
}

/** The series key a chart uses for a sensor: its process parameter, or its sensor id when it has none, and its chamber. */
export function chartSeriesKey(sensor: { id: string; parameter?: string; chamberId?: string }): { parameter: string; chamberId?: string } {
  return { parameter: sensor.parameter ?? sensor.id, chamberId: sensor.chamberId };
}

export function seriesPoints(live: ToolLive | null, parameter: string, chamberId?: string): SpcPoint[] | undefined {
  const match = chartSeries(live, parameter, chamberId);
  if (!match) return undefined;
  return match.points.map((point, index) => ({ index, value: point.value }));
}

/**
 * The center and sigma for each point, from the per-recipe-step segments: the limits the rules engine scored that
 * point against. Undefined when the server sent no segments (counters, older servers).
 */
export function pointLimits(live: ToolLive | null, parameter: string, chamberId?: string): StepLimit[] | undefined {
  const match = chartSeries(live, parameter, chamberId);
  const segments = match?.segments;
  if (!match || !segments || segments.length === 0) return undefined;
  const starts = segments.map((segment) => Date.parse(segment.from));
  let cursor = 0;
  return match.points.map((point) => {
    const at = Date.parse(point.at);
    while (cursor + 1 < segments.length && starts[cursor + 1] <= at) cursor += 1;
    const segment = segments[cursor];
    return {
      mean: segment.center,
      sigma: segment.sigma,
      floor: segment.floor ?? undefined,
      ceiling: segment.ceiling ?? undefined,
      recipe: segment.recipe ?? undefined,
      step: segment.step ?? undefined,
      idle: segment.idle ?? false,
      // Each segment is one recipe step: its index tells two steps with the same name apart.
      segment: cursor,
    };
  });
}

/** The center and sigma the rules engine uses for this series, when the server sent them. */
export function seriesLimits(
  live: ToolLive | null,
  parameter: string,
  chamberId?: string,
): { mean: number; sigma: number; floor?: number; ceiling?: number } | undefined {
  const match = chartSeries(live, parameter, chamberId);
  if (!match || match.center == null || match.sigma == null || !(match.sigma > 0)) return undefined;
  const last = match.segments?.[match.segments.length - 1];
  return { mean: match.center, sigma: match.sigma, floor: last?.floor ?? undefined, ceiling: last?.ceiling ?? undefined };
}

/** The running recipe's readable name from the fleet data; a dash until the first live load. */
export function recipeLabel(live: { recipeName?: string | null } | null | undefined): string {
  return live?.recipeName ?? "—";
}
