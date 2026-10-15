// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company
// Floor geometry for the sites blueprint. Boxes are in a 1280-wide design space and scale uniformly.

import type { SiteDetail, Throughput, ToolFamily, ToolStatus } from "../api/types";

export const DESIGN_WIDTH = 1280;

const PAD = 16;
const GUTTER = 72;
const BAY_H = 460;
const TITLE_H = 42;
const KPI_H = 52;
const MARKER_W = 108;
const MARKER_H = 74;
const FLOW_LINE = 32;
const FLOW_BAND_PAD = 12;
const BADGE = 26;

export type LabelKind = "bay" | "kpi" | "tool" | "flow" | "badge";

export type LabelBox = {
  id: string;
  kind: LabelKind;
  text: string;
  x: number;
  y: number;
  w: number;
  h: number;
  fontSize: number;
  mono: boolean;
  /** Space kept above the words for the tool icon. */
  reservedTop: number;
};

export type FloorTool = {
  id: string;
  name: string;
  family: ToolFamily;
  status: ToolStatus;
  x: number;
  y: number;
  w: number;
  h: number;
};

export type FloorBay = {
  id: string;
  name: string;
  x: number;
  y: number;
  w: number;
  h: number;
  tools: FloorTool[];
};

export type FloorFlow = {
  index: number;
  label: string;
  x1: number;
  y1: number;
  x2: number;
  y2: number;
  badgeX: number;
  badgeY: number;
};

export type FloorLayout = {
  width: number;
  height: number;
  bays: FloorBay[];
  flows: FloorFlow[];
  labels: LabelBox[];
};

type Rect = { x: number; y: number; w: number; h: number };

export function formatPerHour(value: number): string {
  return `${value.toLocaleString("en-US", { minimumFractionDigits: 1, maximumFractionDigits: 1 })}/h`;
}

export function formatPct(value: number): string {
  return `${value.toLocaleString("en-US", { minimumFractionDigits: 1, maximumFractionDigits: 1 })}%`;
}

export function kpiLines(kpis: Throughput): [string, string] {
  return [
    `Wafer starts ${formatPerHour(kpis.waferStartsPerHour)} · Wafer outs ${formatPerHour(kpis.waferOutsPerHour)}`,
    `WIP ${kpis.wip} · Yield ${formatPct(kpis.yieldPct)} · Uptime ${formatPct(kpis.uptimePct)}`,
  ];
}

export function statusWord(status: ToolStatus): string {
  if (status === "alarm") return "Alarm";
  if (status === "warning") return "Watch";
  return "Nominal";
}

export function textWidth(text: string, fontSize: number, mono: boolean): number {
  return text.length * fontSize * (mono ? 0.68 : 0.62);
}

export function labelFits(box: LabelBox): boolean {
  const lines = box.text.split("\n");
  const lineHeight = box.fontSize * 1.35;
  const needed = box.reservedTop + lines.length * lineHeight + 4;
  if (needed > box.h + 0.01) return false;
  const inner = box.w - 8;
  return lines.every((line) => textWidth(line, box.fontSize, box.mono) <= inner + 0.01);
}

export function labelInside(box: LabelBox, width: number, height: number, margin = 8): boolean {
  return (
    box.x >= margin - 0.01 &&
    box.y >= margin - 0.01 &&
    box.x + box.w <= width - margin + 0.01 &&
    box.y + box.h <= height - margin + 0.01
  );
}

export function intersects(a: LabelBox, b: LabelBox, gap = 2): boolean {
  return a.x < b.x + b.w + gap && b.x < a.x + a.w + gap && a.y < b.y + b.h + gap && b.y < a.y + a.h + gap;
}

export function overlappingPairs(boxes: LabelBox[], gap = 2): string[] {
  const hits: string[] = [];
  for (let i = 0; i < boxes.length; i += 1) {
    for (let j = i + 1; j < boxes.length; j += 1) {
      if (intersects(boxes[i], boxes[j], gap)) hits.push(`${boxes[i].id} x ${boxes[j].id}`);
    }
  }
  return hits;
}

export function scaleLayout(layout: FloorLayout, viewportWidth: number): FloorLayout {
  const scale = viewportWidth / layout.width;
  const box = (label: LabelBox): LabelBox => ({
    ...label,
    x: label.x * scale,
    y: label.y * scale,
    w: label.w * scale,
    h: label.h * scale,
    fontSize: label.fontSize * scale,
    reservedTop: label.reservedTop * scale,
  });
  return {
    width: layout.width * scale,
    height: layout.height * scale,
    bays: layout.bays.map((bay) => ({
      ...bay,
      x: bay.x * scale,
      y: bay.y * scale,
      w: bay.w * scale,
      h: bay.h * scale,
      tools: bay.tools.map((tool) => ({
        ...tool,
        x: tool.x * scale,
        y: tool.y * scale,
        w: tool.w * scale,
        h: tool.h * scale,
      })),
    })),
    flows: layout.flows.map((flow) => ({
      ...flow,
      x1: flow.x1 * scale,
      y1: flow.y1 * scale,
      x2: flow.x2 * scale,
      y2: flow.y2 * scale,
      badgeX: flow.badgeX * scale,
      badgeY: flow.badgeY * scale,
    })),
    labels: layout.labels.map(box),
  };
}

function clamp(value: number, min: number, max: number): number {
  return Math.min(max, Math.max(min, value));
}

function port(from: Rect, to: Rect): { x: number; y: number } {
  const fromCenter = { x: from.x + from.w / 2, y: from.y + from.h / 2 };
  const toCenter = { x: to.x + to.w / 2, y: to.y + to.h / 2 };
  const dx = toCenter.x - fromCenter.x;
  const dy = toCenter.y - fromCenter.y;
  if (Math.abs(dx) * from.h >= Math.abs(dy) * from.w) {
    const x = dx >= 0 ? from.x + from.w : from.x;
    const y = Math.abs(dx) < 0.001 ? fromCenter.y : fromCenter.y + (dy / dx) * (x - fromCenter.x);
    return { x, y: clamp(y, from.y + 18, from.y + from.h - 18) };
  }
  const y = dy >= 0 ? from.y + from.h : from.y;
  const x = Math.abs(dy) < 0.001 ? fromCenter.x : fromCenter.x + (dx / dy) * (y - fromCenter.y);
  return { x: clamp(x, from.x + 18, from.x + from.w - 18), y };
}

function badgeBox(id: string, text: string, x: number, y: number): LabelBox {
  return {
    id,
    kind: "badge",
    text,
    x,
    y,
    w: BADGE,
    h: BADGE,
    fontSize: 12,
    mono: true,
    reservedTop: 0,
  };
}

/** Clear space kept around a flow number so two numbers never read as touching. */
const BADGE_GAP = 6;

/** How far each of two crossing flow numbers slides along its own arrow, away from the shared midpoint. */
const CROSSING_SHIFT = 24;

/** Crossing arrows share a midpoint. Step the number off that point until it clears every label. */
function chooseBadge(
  id: string,
  text: string,
  start: { x: number; y: number },
  end: { x: number; y: number },
  taken: LabelBox[],
  width: number,
  height: number,
  preferredShift = 0,
): { x: number; y: number } {
  const midX = (start.x + end.x) / 2;
  const midY = (start.y + end.y) / 2;
  const dx = end.x - start.x;
  const dy = end.y - start.y;
  const length = Math.hypot(dx, dy) || 1;
  const px = -dy / length;
  const py = dx / length;
  const tx = dx / length;
  const ty = dy / length;
  const offsets: Array<[number, number]> = [];
  if (preferredShift !== 0) {
    // Crossing arrows: push the pair apart along their own lines so the two numbers sit side by side.
    const sign = Math.sign(tx) === 0 ? 1 : Math.sign(tx);
    offsets.push([tx * preferredShift * sign, ty * preferredShift * sign]);
  }
  // Slide along the arrow first so the number stays on its own line; step sideways only as a last resort.
  for (const dist of [0, 16, 20, 24, 28, 32, 36, 40, 44, 48, 56, 64]) {
    offsets.push([tx * dist, ty * dist], [-tx * dist, -ty * dist]);
  }
  offsets.push([0, 0]);
  for (const dist of [20, 36, 52, 68, 84, 100, 116, 132]) {
    offsets.push([px * dist, py * dist], [-px * dist, -py * dist]);
  }
  const fallback = { x: midX - BADGE / 2, y: midY - BADGE / 2 };
  for (const [ox, oy] of offsets) {
    const spot = { x: midX + ox - BADGE / 2, y: midY + oy - BADGE / 2 };
    const box = badgeBox(id, text, spot.x, spot.y);
    const padded = { ...box, x: box.x - BADGE_GAP, y: box.y - BADGE_GAP, w: box.w + BADGE_GAP * 2, h: box.h + BADGE_GAP * 2 };
    const clear = labelInside(box, width, height, 8) && taken.every((other) => !intersects(padded, other));
    if (clear) return spot;
  }
  return fallback;
}

function separateTools(tools: FloorTool[], usable: Rect): void {
  const gap = 4;
  for (let pass = 0; pass < 30; pass += 1) {
    let moved = false;
    for (let i = 0; i < tools.length; i += 1) {
      for (let j = i + 1; j < tools.length; j += 1) {
        const a = tools[i];
        const b = tools[j];
        const overlapX = Math.min(a.x + a.w, b.x + b.w) - Math.max(a.x, b.x);
        const overlapY = Math.min(a.y + a.h, b.y + b.h) - Math.max(a.y, b.y);
        if (overlapX + gap <= 0 || overlapY + gap <= 0) continue;
        const lower = a.y <= b.y ? b : a;
        const maxY = usable.y + usable.h - lower.h;
        const next = lower.y + overlapY + gap;
        if (next <= maxY) {
          lower.y = next;
        } else {
          lower.y = maxY;
          const other = lower === a ? b : a;
          const shift = overlapX + gap;
          if (lower.x >= other.x) lower.x = Math.min(lower.x + shift, usable.x + usable.w - lower.w);
          else lower.x = Math.max(lower.x - shift, usable.x);
        }
        moved = true;
      }
    }
    if (!moved) return;
  }
}

export function layoutFloor(site: SiteDetail): FloorLayout {
  const columns = 2;
  const rows = Math.max(1, Math.ceil(site.bays.length / columns));
  const bayW = (DESIGN_WIDTH - PAD * 2 - GUTTER) / columns;
  const flowH = FLOW_BAND_PAD * 2 + Math.max(site.flows.length, 1) * FLOW_LINE;
  const height = PAD + rows * BAY_H + Math.max(0, rows - 1) * GUTTER + PAD + flowH + PAD;
  const labels: LabelBox[] = [];

  const bays: FloorBay[] = site.bays.map((bay, index) => {
    const column = index % columns;
    const row = Math.floor(index / columns);
    const rect = {
      x: PAD + column * (bayW + GUTTER),
      y: PAD + row * (BAY_H + GUTTER),
      w: bayW,
      h: BAY_H,
    };
    labels.push({
      id: `bay-${bay.id}`,
      kind: "bay",
      text: bay.name,
      x: rect.x + 12,
      y: rect.y + 8,
      w: rect.w - 24,
      h: 28,
      fontSize: 16,
      mono: false,
      reservedTop: 0,
    });
    const [line1, line2] = kpiLines(bay.kpis);
    labels.push({
      id: `kpi-${bay.id}`,
      kind: "kpi",
      text: `${line1}\n${line2}`,
      x: rect.x + 12,
      y: rect.y + rect.h - KPI_H + 4,
      w: rect.w - 24,
      h: KPI_H - 8,
      fontSize: 13,
      mono: false,
      reservedTop: 0,
    });

    const usable = {
      x: rect.x + 12,
      y: rect.y + TITLE_H,
      w: rect.w - 24,
      h: rect.h - TITLE_H - KPI_H,
    };
    const spanX = Math.max(usable.w - MARKER_W, 1);
    const spanY = Math.max(usable.h - MARKER_H, 1);
    const tools: FloorTool[] = bay.tools.map((tool) => ({
      id: tool.id,
      name: tool.name,
      family: tool.family,
      status: tool.status,
      x: usable.x + (clamp(tool.x, 0, 100) / 100) * spanX,
      y: usable.y + (clamp(tool.y, 0, 100) / 100) * spanY,
      w: MARKER_W,
      h: MARKER_H,
    }));
    separateTools(tools, usable);
    for (const tool of tools) {
      labels.push({
        id: `tool-${tool.id}`,
        kind: "tool",
        text: `${statusWord(tool.status)}\n${tool.id}`,
        x: tool.x,
        y: tool.y,
        w: tool.w,
        h: tool.h,
        fontSize: 12,
        mono: true,
        reservedTop: 28,
      });
    }
    return { id: bay.id, name: bay.name, ...rect, tools };
  });

  const byId = new Map(bays.map((bay) => [bay.id, bay]));
  const bandTop = PAD + rows * BAY_H + Math.max(0, rows - 1) * GUTTER + PAD;
  const flows: FloorFlow[] = [];
  const midpoints = site.flows.map((flow) => {
    const from = byId.get(flow.fromBayId);
    const to = byId.get(flow.toBayId);
    if (!from || !to) return null;
    const a = port(from, to);
    const b = port(to, from);
    return { x: (a.x + b.x) / 2, y: (a.y + b.y) / 2 };
  });
  const shiftFor = (index: number): number => {
    const mine = midpoints[index];
    if (!mine) return 0;
    const group = midpoints
      .map((point, i) => ({ point, i }))
      .filter(({ point }) => point && Math.hypot(point.x - mine.x, point.y - mine.y) < 4)
      .map(({ i }) => i);
    if (group.length < 2) return 0;
    return group.indexOf(index) % 2 === 0 ? -CROSSING_SHIFT : CROSSING_SHIFT;
  };
  site.flows.forEach((flow, index) => {
    const sentence = `${index + 1}. ${flow.label}`;
    labels.push({
      id: `flow-${flow.fromBayId}-${flow.toBayId}`,
      kind: "flow",
      text: sentence,
      x: PAD,
      y: bandTop + FLOW_BAND_PAD + index * FLOW_LINE,
      w: DESIGN_WIDTH - PAD * 2,
      h: FLOW_LINE - 6,
      fontSize: 13,
      mono: false,
      reservedTop: 0,
    });
    const from = byId.get(flow.fromBayId);
    const to = byId.get(flow.toBayId);
    if (!from || !to) return;
    const start = port(from, to);
    const end = port(to, from);
    const badgeId = `badge-${flow.fromBayId}-${flow.toBayId}`;
    const badge = chooseBadge(badgeId, String(index + 1), start, end, labels, DESIGN_WIDTH, height, shiftFor(index));
    flows.push({
      index: index + 1,
      label: flow.label,
      x1: start.x,
      y1: start.y,
      x2: end.x,
      y2: end.y,
      badgeX: badge.x,
      badgeY: badge.y,
    });
    labels.push(badgeBox(badgeId, String(index + 1), badge.x, badge.y));
  });

  return { width: DESIGN_WIDTH, height, bays, flows, labels };
}
