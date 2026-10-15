// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company
import type { CausalTrace, TraceNode } from "../api/types";

export type TraceLine = {
  eventId: string;
  type: string;
  summary: string;
  occurredAt: string;
  toolName: string | null;
  value: number | null;
  limitLow: number | null;
  limitHigh: number | null;
  focus: boolean;
  readingCount: number | null;
};

const TELEMETRY = "telemetry.reading";

export function traceLines(trace: CausalTrace): TraceLine[] {
  const lines: TraceLine[] = [];
  for (const ancestor of trace.ancestors) {
    lines.push(toLine(ancestor, trace.focusEventId, null));
  }
  appendNode(trace.tree, trace.focusEventId, lines);
  return lines;
}

export function valueVsLimit(line: Pick<TraceLine, "value" | "limitLow" | "limitHigh">): string | null {
  if (line.value == null || !Number.isFinite(line.value)) return null;
  const value = formatNumber(line.value);
  if (line.limitLow == null || line.limitHigh == null || !Number.isFinite(line.limitLow) || !Number.isFinite(line.limitHigh)) {
    return `Value ${value}`;
  }
  return `Value ${value}, limit ${formatNumber(line.limitLow)} to ${formatNumber(line.limitHigh)}`;
}

function appendNode(node: TraceNode, focusId: string, lines: TraceLine[]): void {
  const telemetry = node.children.filter((child) => child.type === TELEMETRY);
  const rest = node.children.filter((child) => child.type !== TELEMETRY);
  if (telemetry.length > 1 && rest.length > 0) {
    lines.push(toLine(node, focusId, null));
    const sample = byTime(telemetry).find((child) => child.value != null) ?? telemetry[0];
    lines.push(toLine(sample, focusId, telemetry.length));
    lines[lines.length - 1].summary = "Readings streamed in";
    lines[lines.length - 1].focus = telemetry.some((child) => child.eventId === focusId);
    for (const child of byTime(rest)) appendNode(child, focusId, lines);
    return;
  }

  lines.push(toLine(node, focusId, null));
  for (const child of byTime(node.children)) appendNode(child, focusId, lines);
}

function toLine(node: TraceNode, focusId: string, readingCount: number | null): TraceLine {
  return {
    eventId: node.eventId,
    type: node.type,
    summary: node.summary,
    occurredAt: node.occurredAt,
    toolName: node.toolName ?? null,
    value: node.value ?? null,
    limitLow: node.limitLow ?? null,
    limitHigh: node.limitHigh ?? null,
    focus: node.eventId === focusId,
    readingCount,
  };
}

function byTime(nodes: TraceNode[]): TraceNode[] {
  return [...nodes].sort((left, right) => left.occurredAt.localeCompare(right.occurredAt) || left.eventId.localeCompare(right.eventId));
}

function formatNumber(value: number): string {
  if (Number.isInteger(value)) return String(value);
  return String(Math.round(value * 1000) / 1000);
}
