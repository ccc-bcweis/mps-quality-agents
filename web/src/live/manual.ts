// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company
import type { LiveSeries, ManualReading, ToolLive } from "../api/types";
import type { SensorStatus } from "../fleet/telemetry";

export const INSTRUMENT_WORDS: Record<string, string> = {
  oscilloscope: "oscilloscope",
  rfPowerMeter: "RF power meter",
  particleCounter: "particle counter",
  thermocoupleLogger: "thermocouple logger",
};

export function instrumentWords(instrument: string): string {
  return INSTRUMENT_WORDS[instrument] ?? instrument;
}

/** Latest manual reading (by observed time) for one parameter, preferring the same chamber. */
export function latestManual(readings: ManualReading[] | null | undefined, parameter: string, chamberId?: string | null): ManualReading | undefined {
  const rows = (readings ?? []).filter((row) => row.parameter === parameter && (!chamberId || !row.chamberId || row.chamberId === chamberId));
  return rows.sort((a, b) => Date.parse(b.observedAt) - Date.parse(a.observedAt))[0];
}

export type ManualTile = { value: number; unit: string; status: SensorStatus; manual: { label: string; title: string } };

/** The schematic tile for a sensor with a manual reading: the printed value, a manual badge, colored by the printed spec. */
export function manualTile(reading: ManualReading): ManualTile {
  return {
    value: reading.value,
    unit: reading.unit,
    status: reading.outOfSpec ? "outOfSpec" : "inSpec",
    manual: {
      label: "Manual",
      title: `${reading.source} Observed ${reading.observedAt}.`,
    },
  };
}

export type ManualMarker = { index: number; value: number; clamped: boolean; label: string; outOfSpec: boolean };

/**
 * Where the manual reading sits on the live chart. A reading older than the window is pinned to the left edge and flagged,
 * one newer than the last point is pinned to the right edge. The real observed time stays on the record.
 */
export function manualMarker(series: LiveSeries | undefined, reading: ManualReading | undefined): ManualMarker | undefined {
  if (!reading) return undefined;
  const label = `Manual reading ${reading.value} ${reading.unit} (${instrumentWords(reading.instrument)})${reading.active ? "" : ", resolved"}`;
  // A resolved reading stays on the chart as history but no longer draws in alarm color.
  reading = { ...reading, outOfSpec: reading.outOfSpec && reading.active };
  const points = series?.points ?? [];
  if (points.length === 0) return { index: 0, value: reading.value, clamped: reading.olderThanWindow, label, outOfSpec: reading.outOfSpec };
  const at = Date.parse(reading.observedAt);
  const first = Date.parse(points[0].at);
  const last = Date.parse(points[points.length - 1].at);
  if (at < first) return { index: 0, value: reading.value, clamped: true, label, outOfSpec: reading.outOfSpec };
  if (at >= last) return { index: points.length - 1, value: reading.value, clamped: false, label, outOfSpec: reading.outOfSpec };
  let index = 0;
  for (let i = 0; i < points.length; i++) {
    if (Date.parse(points[i].at) <= at) index = i;
  }
  return { index, value: reading.value, clamped: false, label, outOfSpec: reading.outOfSpec };
}

export function seriesFor(live: ToolLive | null, parameter: string, chamberId?: string | null): LiveSeries | undefined {
  const rows = (live?.series ?? []).filter((series) => series.parameter === parameter && series.points.length > 0);
  return rows.find((series) => chamberId && series.chamberId === chamberId) ?? rows[0];
}

/** Earliest time the chart window holds for this series; a reading before it will not plot in place. */
export function windowStart(live: ToolLive | null, parameter: string, chamberId?: string | null): string | undefined {
  return seriesFor(live, parameter, chamberId)?.points[0]?.at;
}

export function isOlderThanWindow(observedAt: string, start: string | undefined): boolean {
  if (!start) return false;
  const at = Date.parse(observedAt);
  return Number.isFinite(at) && at < Date.parse(start);
}
