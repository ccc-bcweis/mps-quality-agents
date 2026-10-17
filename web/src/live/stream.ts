// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company
// Pure patches for the fleet hub messages. A page keeps its first load and applies each message to it.
import type { FleetLive, FleetLiveTool, LiveReading, LiveSeries, LiveStatus, ToolLive } from "../api/types";

/** Points kept per chart series; the same window GET /api/tools/{id}/live returns. */
export const LIVE_WINDOW_POINTS = 600;

/** toolLive: the tool's latest readings and only the new points per series. resync asks the page to load again. */
export type ToolLiveDelta = {
  toolId: string;
  siteId: string;
  simulatedNow: string;
  status: LiveStatus;
  readings: LiveReading[];
  series: LiveSeries[];
  recipeName?: string | null;
  resync: boolean;
  /** A bench printout reading arrived for this tool. */
  manual?: boolean;
};

/** fleetLive: only the tools whose readings changed. */
export type FleetLiveDelta = { simulatedNow: string; tools: FleetLiveTool[] };

export type AlertCleared = { alertId: string; toolId?: string; clearedAt?: string | null };

const seriesKey = (series: { parameter: string; chamberId: string }) => `${series.parameter}|${series.chamberId}`;

/** Appends the new points to each series (oldest dropped past the window). Null when there is no first load to patch. */
export function applyToolDelta(previous: ToolLive | null, delta: ToolLiveDelta, maxPoints = LIVE_WINDOW_POINTS): ToolLive | null {
  if (!previous || delta.resync || previous.toolId !== delta.toolId) return null;
  const byKey = new Map(previous.series.map((series) => [seriesKey(series), series]));
  for (const incoming of delta.series) {
    const key = seriesKey(incoming);
    const current = byKey.get(key);
    const lastAt = current && current.points.length > 0 ? Date.parse(current.points[current.points.length - 1].at) : Number.NEGATIVE_INFINITY;
    const fresh = incoming.points.filter((point) => Date.parse(point.at) > lastAt);
    const points = [...(current?.points ?? []), ...fresh];
    byKey.set(key, {
      parameter: incoming.parameter,
      chamberId: incoming.chamberId,
      points: points.length > maxPoints ? points.slice(points.length - maxPoints) : points,
      center: incoming.center ?? null,
      sigma: incoming.sigma ?? null,
    });
  }
  const series = [...byKey.values()].sort((left, right) =>
    left.parameter === right.parameter ? left.chamberId.localeCompare(right.chamberId) : left.parameter.localeCompare(right.parameter),
  );
  return {
    ...previous,
    simulatedNow: delta.simulatedNow,
    status: delta.status,
    readings: delta.readings,
    series,
    recipeName: delta.recipeName ?? previous.recipeName,
  };
}

/** Replaces the changed tools in the plant snapshot. Null when there is no first load to patch. */
export function applyFleetDelta(previous: FleetLive | null, delta: FleetLiveDelta): FleetLive | null {
  if (!previous) return null;
  const byId = new Map(previous.tools.map((tool) => [tool.toolId, tool]));
  for (const tool of delta.tools) byId.set(tool.toolId, tool);
  return {
    simulatedNow: delta.simulatedNow,
    tools: [...byId.values()].sort((left, right) => left.toolId.localeCompare(right.toolId)),
  };
}

/** Puts a changed record in place, or first in the list when it is new. Returns the same list when nothing changed. */
export function upsertRecord<T extends { id: string }>(list: T[], record: T): T[] {
  const index = list.findIndex((item) => item.id === record.id);
  if (index < 0) return [record, ...list];
  if (list[index] === record) return list;
  const next = list.slice();
  next[index] = { ...list[index], ...record };
  return next;
}

/** Marks one alert cleared from the alertCleared message. */
export function clearAlert<T extends { id: string; status?: string; clearedAt?: string | null }>(list: T[], cleared: AlertCleared): T[] {
  const index = list.findIndex((item) => item.id === cleared.alertId);
  if (index < 0) return list;
  const next = list.slice();
  next[index] = { ...list[index], status: "cleared", clearedAt: cleared.clearedAt ?? list[index].clearedAt ?? null };
  return next;
}

/** Narrow check that a hub payload is a record with an id. */
export function isRecord(payload: unknown): payload is { id: string; toolId?: string | null; siteId?: string | null } {
  return typeof payload === "object" && payload !== null && typeof (payload as { id?: unknown }).id === "string";
}
