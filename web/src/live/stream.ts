// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company
// Pure patches for the fleet hub messages. A page keeps its first load and applies each message to it.
import type { FleetLive, FleetLiveTool, LiveLimitSegment, LiveReading, LiveSeries, LiveStatus, ToolLive } from "../api/types";

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

/**
 * Appends the new points to each series, oldest dropped past the window: the window the first load reported
 * (windowPoints, from simulation.yaml). Each series counts the readings that rolled off in first, so reading numbers
 * keep growing and the chart scrolls. Null when there is no first load to patch.
 */
export function applyToolDelta(previous: ToolLive | null, delta: ToolLiveDelta, maxPoints = previous?.windowPoints ?? Number.POSITIVE_INFINITY): ToolLive | null {
  if (!previous || delta.resync || previous.toolId !== delta.toolId) return null;
  const byKey = new Map(previous.series.map((series) => [seriesKey(series), series]));
  for (const incoming of delta.series) {
    const key = seriesKey(incoming);
    const current = byKey.get(key);
    const lastAt = current && current.points.length > 0 ? Date.parse(current.points[current.points.length - 1].at) : Number.NEGATIVE_INFINITY;
    const fresh = incoming.points.filter((point) => Date.parse(point.at) > lastAt);
    const points = [...(current?.points ?? []), ...fresh];
    const kept = points.length > maxPoints ? points.slice(points.length - maxPoints) : points;
    byKey.set(key, {
      parameter: incoming.parameter,
      chamberId: incoming.chamberId,
      points: kept,
      first: (current?.first ?? 0) + (points.length - kept.length),
      center: incoming.center ?? null,
      sigma: incoming.sigma ?? null,
      segments: mergeSegments(current?.segments, incoming.segments, kept[0]?.at),
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

/**
 * Appends the newer per-step limits and drops the ones that end before the oldest kept point, so each point keeps
 * the limits of the recipe step it was read in.
 */
export function mergeSegments(
  previous: LiveLimitSegment[] | null | undefined,
  incoming: LiveLimitSegment[] | null | undefined,
  firstAt: string | undefined,
): LiveLimitSegment[] | null {
  if (!previous?.length && !incoming?.length) return incoming ?? previous ?? null;
  const merged = [...(previous ?? [])];
  for (const segment of incoming ?? []) {
    const last = merged[merged.length - 1];
    const at = Date.parse(segment.from);
    if (last && at < Date.parse(last.from)) continue;
    if (last && at === Date.parse(last.from)) {
      merged[merged.length - 1] = segment;
      continue;
    }
    // Same step continuing (a delta inside the step the window already ends in): keep the step's first point.
    if (last && sameStep(last, segment)) continue;
    merged.push(segment);
  }
  if (!firstAt) return merged;
  const first = Date.parse(firstAt);
  let start = 0;
  while (start + 1 < merged.length && Date.parse(merged[start + 1].from) <= first) start += 1;
  return merged.slice(start);
}

/** Two segments of one recipe step: same recipe, step and limits. A new step always starts a new segment. */
function sameStep(left: LiveLimitSegment, right: LiveLimitSegment): boolean {
  return (
    left.center === right.center &&
    left.sigma === right.sigma &&
    (left.recipe ?? null) === (right.recipe ?? null) &&
    (left.step ?? null) === (right.step ?? null)
  );
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

/** The fields that tell one investigation round apart: its incident and round, and its stage. */
type InvestigationRound = { id: string; incidentId: string; round?: number; stage: string };

/**
 * Puts a changed investigation in place, one entry per incident and round (#378). While a round gathers, the server
 * streams a "started" placeholder with a temporary id; the saved investigation then arrives with its own id. Upserting
 * by id alone kept both, so Linked records showed "Started" next to "Recommendation ready" for one investigation.
 * The saved record replaces the placeholder of its round, and a late placeholder never replaces a saved record.
 */
export function upsertInvestigation<T extends InvestigationRound>(list: T[], record: T): T[] {
  const sameRound = (item: T) => item.id !== record.id && item.incidentId === record.incidentId && (item.round ?? 1) === (record.round ?? 1);
  const placeholder = (item: T) => sameRound(item) && item.stage === "started";
  if (record.stage === "started" && list.some((item) => sameRound(item) && item.stage !== "started")) return list;
  const at = list.findIndex(placeholder);
  if (at >= 0 && !list.some((item) => item.id === record.id)) {
    // The saved record takes its placeholder's place, so the entry updates where it was.
    return list.map((item, i) => (i === at ? record : item)).filter((item, i) => i === at || !placeholder(item));
  }
  return upsertRecord(list.filter((item) => !placeholder(item)), record);
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
