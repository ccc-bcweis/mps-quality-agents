// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company
import type { ToolFamily } from "../api/types";

/** Narrowest chamber or station panel. Two fit side by side only when the row is at least this twice, plus the gap. */
export const CHAMBER_MIN_PX = 620;

/** Load-lock panel cap. At 1280px and up the row is wider than this, so the drawing stays at this size. */
export const HANDLER_PANEL_PX = 700;

/** Narrowest tool-counter card. Text wraps inside the card instead of clipping. */
export const COUNTER_MIN_PX = 220;

export const SENSOR_FONT_PX = 11;
export const VALUE_FONT_PX = 12;

export type AnchorBox = {
  anchor: string;
  x: number;
  y: number;
  w: number;
  h: number;
};

export type ViewBox = { width: number; height: number };

export type PanelKind = "handler" | "chamber" | "station" | "counters";

export type PanelArt = "handler" | "etch" | "deposition" | "strip" | "cleanBath" | "cleanManifold" | "counters";

export type ArtKind = "text" | "rect" | "line" | "circle" | "ellipse" | "showerhead" | "match" | "pump" | "throttle" | "gauge" | "plasma" | "megasonic";

export type ArtPiece = {
  id: string;
  kind: ArtKind;
  x: number;
  y: number;
  w: number;
  h: number;
  label?: string;
};

export type SchematicPanel = {
  id: string;
  title: string;
  kind: PanelKind;
  art: PanelArt;
  view: ViewBox;
  boxes: AnchorBox[];
};

export type FaceplateText = { label: string; id: string; value: string };

const VIEW_W = 700;
const BOX_H = 92;
const ROW_GAP = 12;
const PAD_LEFT = 120;
const PAD_RIGHT = 16;
const FIRST_CENTER = 86;
const ROW_PITCH = 108;

const POINT_LABEL: Record<string, string> = {
  "chuck.esc": "ESC temperature",
  "chuck.heater": "Heater TC",
  "chuck.temperature": "Platen TC",
  "chuck.bath": "Bath TC",
  "chuck.edgeRing": "Edge ring",
  "chuck.megasonic": "Megasonic",
  "chamber.wall": "Wall TC",
  "chamber.pressure": "Chamber pressure",
  "chamber.etchRate": "Etch rate",
  "chamber.endpoint": "Endpoint",
  "chamber.particles": "Particles",
  "chamber.thickness": "Thickness",
  "chamber.uniformity": "Uniformity",
  "chamber.ashRate": "Ash rate",
  "chamber.residue": "Residue",
  "rf.forward": "RF FWD",
  "rf.reflected": "RF RFL",
  "rf.source.forward": "Source RF FWD",
  "rf.source.reflected": "Source RF RFL",
  "rf.bias.forward": "Bias RF FWD",
  "rf.bias.reflected": "Bias RF RFL",
  "rf.remote.forward": "Remote RF FWD",
  "rf.remote.reflected": "Remote RF RFL",
  "gas.sih4": "MFC SiH4",
  "gas.n2o": "MFC N2O",
  "gas.nh3": "MFC NH3",
  "gas.cf4": "MFC CF4",
  "gas.chf3": "MFC CHF3",
  "gas.o2": "MFC O2",
  "gas.ar": "MFC Ar",
  "gas.n2h2": "MFC N2H2",
  "gas.sc1": "MFC SC1",
  "gas.sc2": "MFC SC2",
  "gas.diw": "MFC DIW",
  "vacuum.vibration": "Pump vibration",
  "handler.center": "Center offset",
  "handler.runout": "Chuck runout",
  runRate: "Run rate",
  throughput: "Throughput",
  output: "Output",
  yield: "Yield",
};

type Point = { x: number; y: number; w: number; h: number };
type RowItem = { key: string; w: number };

function placeRow(row: number, items: RowItem[], extra = 0): Record<string, Point> {
  const total = items.reduce((sum, item) => sum + item.w, 0) + ROW_GAP * (items.length - 1);
  const available = VIEW_W - PAD_LEFT - PAD_RIGHT;
  if (total > available) {
    throw new Error(`Schematic row does not fit: ${items.map((item) => item.key).join(", ")}`);
  }
  let cursor = PAD_LEFT + Math.floor((available - total) / 2);
  const y = FIRST_CENTER + row * ROW_PITCH + extra;
  const points: Record<string, Point> = {};
  for (const item of items) {
    points[item.key] = { x: cursor + item.w / 2, y, w: item.w, h: BOX_H };
    cursor += item.w + ROW_GAP;
  }
  return points;
}

function boxesFrom(prefix: string, points: Record<string, Point>): AnchorBox[] {
  return Object.entries(points).map(([body, point]) => ({ anchor: prefix ? `${prefix}.${body}` : body, ...point }));
}

function chamberPanel(slot: "a" | "b", art: PanelArt, points: Record<string, Point>, height: number): SchematicPanel {
  const title = slot === "a" ? "Chamber A" : "Chamber B";
  return {
    id: `chamber-${slot}`,
    title,
    kind: "chamber",
    art,
    view: { width: VIEW_W, height },
    boxes: boxesFrom(slot, points),
  };
}

function stationPanel(id: string, title: string, art: PanelArt, points: Record<string, Point>, height: number): SchematicPanel {
  return {
    id,
    title,
    kind: "station",
    art,
    view: { width: VIEW_W, height },
    boxes: boxesFrom("a", points),
  };
}

const HANDLER_VIEW: ViewBox = { width: VIEW_W, height: 260 };

function handlerPanel(boxes: AnchorBox[]): SchematicPanel {
  return {
    id: "handler",
    title: "Load lock and wafer handler",
    kind: "handler",
    art: "handler",
    view: HANDLER_VIEW,
    boxes,
  };
}

const DUAL_HANDLER: AnchorBox[] = [
  { anchor: "a.handler.center", x: 480, y: 78, w: 300, h: BOX_H },
  { anchor: "b.handler.center", x: 480, y: 186, w: 300, h: BOX_H },
];

function counterBoxes(): AnchorBox[] {
  const keys = ["tool.runRate", "tool.throughput", "tool.output", "tool.yield"] as const;
  const width = 150;
  const gap = 16;
  const total = keys.length * width + (keys.length - 1) * gap;
  let cursor = Math.floor((VIEW_W - total) / 2);
  return keys.map((anchor) => {
    const box = { anchor, x: cursor + width / 2, y: 40, w: width, h: 64 };
    cursor += width + gap;
    return box;
  });
}

function countersPanel(): SchematicPanel {
  return {
    id: "counters",
    title: "Tool counters",
    kind: "counters",
    art: "counters",
    view: { width: VIEW_W, height: 80 },
    boxes: counterBoxes(),
  };
}

const ETCH_POINTS = {
  ...placeRow(0, [
    { key: "chamber.wall", w: 200 },
    { key: "chamber.pressure", w: 210 },
  ]),
  ...placeRow(1, [
    { key: "chuck.esc", w: 220 },
    { key: "chuck.edgeRing", w: 220 },
  ]),
  ...placeRow(2, [
    { key: "chamber.etchRate", w: 168 },
    { key: "chamber.endpoint", w: 176 },
    { key: "chamber.particles", w: 176 },
  ]),
  ...placeRow(3, [
    { key: "gas.cf4", w: 128 },
    { key: "gas.chf3", w: 128 },
    { key: "gas.o2", w: 128 },
    { key: "gas.ar", w: 128 },
  ]),
  ...placeRow(4, [
    { key: "rf.source.forward", w: 230 },
    { key: "rf.source.reflected", w: 230 },
  ]),
  ...placeRow(5, [
    { key: "rf.bias.forward", w: 230 },
    { key: "rf.bias.reflected", w: 230 },
  ]),
  ...placeRow(6, [{ key: "vacuum.vibration", w: 260 }], 24),
};

const DEPOSITION_POINTS = {
  ...placeRow(0, [
    { key: "chamber.pressure", w: 210 },
    { key: "chuck.heater", w: 230 },
  ]),
  ...placeRow(1, [
    { key: "chamber.thickness", w: 168 },
    { key: "chamber.uniformity", w: 188 },
    { key: "chamber.particles", w: 168 },
  ]),
  ...placeRow(2, [
    { key: "gas.sih4", w: 160 },
    { key: "gas.n2o", w: 160 },
    { key: "gas.nh3", w: 160 },
  ]),
  ...placeRow(3, [
    { key: "rf.forward", w: 230 },
    { key: "rf.reflected", w: 230 },
  ]),
  ...placeRow(4, [{ key: "vacuum.vibration", w: 260 }], 24),
};

const STRIP_POINTS = {
  ...placeRow(0, [
    { key: "chamber.pressure", w: 200 },
    { key: "chuck.temperature", w: 230 },
  ]),
  ...placeRow(1, [
    { key: "chamber.ashRate", w: 220 },
    { key: "chamber.particles", w: 210 },
  ]),
  ...placeRow(2, [
    { key: "gas.o2", w: 200 },
    { key: "gas.n2h2", w: 240 },
  ]),
  ...placeRow(3, [
    { key: "rf.remote.forward", w: 230 },
    { key: "rf.remote.reflected", w: 250 },
  ]),
  ...placeRow(4, [{ key: "vacuum.vibration", w: 260 }], 24),
};

const BATH_POINTS = {
  ...placeRow(0, [
    { key: "chamber.pressure", w: 220 },
    { key: "chuck.bath", w: 240 },
  ]),
  ...placeRow(1, [
    { key: "handler.runout", w: 230 },
    { key: "chuck.megasonic", w: 240 },
  ]),
  ...placeRow(2, [
    { key: "chamber.particles", w: 200 },
    { key: "chamber.residue", w: 200 },
  ]),
};

const MANIFOLD_POINTS = {
  ...placeRow(0, [{ key: "gas.sc1", w: 280 }]),
  ...placeRow(1, [{ key: "gas.sc2", w: 280 }]),
  ...placeRow(2, [{ key: "gas.diw", w: 280 }]),
  ...placeRow(3, [{ key: "vacuum.vibration", w: 280 }], 16),
};

const ETCH_H = 836;
const DEPOSITION_H = 640;
const STRIP_H = 640;
const BATH_H = 400;
const MANIFOLD_H = 540;

const PANELS: Record<ToolFamily, SchematicPanel[]> = {
  etch: [
    handlerPanel(DUAL_HANDLER),
    chamberPanel("a", "etch", ETCH_POINTS, ETCH_H),
    chamberPanel("b", "etch", ETCH_POINTS, ETCH_H),
    countersPanel(),
  ],
  deposition: [
    handlerPanel(DUAL_HANDLER),
    chamberPanel("a", "deposition", DEPOSITION_POINTS, DEPOSITION_H),
    chamberPanel("b", "deposition", DEPOSITION_POINTS, DEPOSITION_H),
    countersPanel(),
  ],
  strip: [
    handlerPanel(DUAL_HANDLER),
    chamberPanel("a", "strip", STRIP_POINTS, STRIP_H),
    chamberPanel("b", "strip", STRIP_POINTS, STRIP_H),
    countersPanel(),
  ],
  clean: [
    handlerPanel([]),
    stationPanel("bath", "Bath and spin chuck", "cleanBath", BATH_POINTS, BATH_H),
    stationPanel("manifold", "Chemical manifold", "cleanManifold", MANIFOLD_POINTS, MANIFOLD_H),
    countersPanel(),
  ],
};

export function schematicPanels(family: ToolFamily): SchematicPanel[] {
  return PANELS[family];
}

export function chamberGridTemplate(): string {
  return `repeat(auto-fit, minmax(min(100%, ${CHAMBER_MIN_PX}px), 1fr))`;
}

export function counterGridTemplate(): string {
  return `repeat(auto-fit, minmax(min(100%, ${COUNTER_MIN_PX}px), 1fr))`;
}

export function anchorBody(anchor: string): string {
  return anchor.replace(/^(?:a|b|tool)\./, "");
}

export function sensorLabel(anchor: string): string {
  return POINT_LABEL[anchorBody(anchor)] ?? anchor;
}

export function slotWord(anchor: string): string {
  if (anchor.startsWith("a.")) return "A";
  if (anchor.startsWith("b.")) return "B";
  return "Tool";
}

export function overlappingPairs(boxes: AnchorBox[]): string[] {
  const hits: string[] = [];
  for (let left = 0; left < boxes.length; left += 1) {
    for (let right = left + 1; right < boxes.length; right += 1) {
      const a = boxes[left];
      const b = boxes[right];
      const gapX = Math.abs(a.x - b.x) - a.w / 2 - b.w / 2;
      const gapY = Math.abs(a.y - b.y) - a.h / 2 - b.h / 2;
      if (gapX < 8 && gapY < 8) hits.push(`${a.anchor}|${b.anchor}`);
    }
  }
  return hits;
}

export function anchorsOutsideView(boxes: AnchorBox[], view: ViewBox, margin = 0): string[] {
  return boxes
    .filter((box) => {
      const left = box.x - box.w / 2;
      const right = box.x + box.w / 2;
      const top = box.y - box.h / 2;
      const bottom = box.y + box.h / 2;
      return left < margin || top < margin || right > view.width - margin || bottom > view.height - margin;
    })
    .map((box) => box.anchor)
    .sort();
}

export function anchorMismatch(expected: Iterable<string>, drawn: Iterable<string>): {
  missingOnDrawing: string[];
  extraOnDrawing: string[];
} {
  const want = new Set(expected);
  const have = new Set(drawn);
  return {
    missingOnDrawing: [...want].filter((anchor) => !have.has(anchor)).sort(),
    extraOnDrawing: [...have].filter((anchor) => !want.has(anchor)).sort(),
  };
}

type Rect = { x: number; y: number; w: number; h: number };

function byBody(boxes: AnchorBox[]): Record<string, AnchorBox> {
  return Object.fromEntries(boxes.map((box) => [anchorBody(box.anchor), box]));
}

function textPiece(id: string, label: string, x: number, y: number, w: number): ArtPiece {
  return { id, kind: "text", x, y, w, h: 18, label };
}

function beside(box: AnchorBox, id: string, label: string, yOffset = 0): ArtPiece {
  const width = Math.max(72, Math.ceil(label.length * 7.6) + 4);
  return textPiece(id, label, 12, box.y - 9 + yOffset, width);
}

function groupRect(id: string, group: AnchorBox[], pad = 6): ArtPiece {
  const left = Math.min(...group.map((box) => box.x - box.w / 2)) - pad;
  const right = Math.max(...group.map((box) => box.x + box.w / 2)) + pad;
  const top = Math.min(...group.map((box) => box.y - box.h / 2)) - pad;
  const bottom = Math.max(...group.map((box) => box.y + box.h / 2)) + pad;
  return { id, kind: "rect", x: left, y: top, w: right - left, h: bottom - top };
}

function present(map: Record<string, AnchorBox>, keys: string[]): AnchorBox[] {
  return keys.map((key) => map[key]).filter((box): box is AnchorBox => Boolean(box));
}

function flowArt(map: Record<string, AnchorBox>, options: { showerhead: boolean; plasma: boolean; heater: boolean }): ArtPiece[] {
  const pieces: ArtPiece[] = [];
  if (options.plasma) {
    pieces.push(textPiece("remote-plasma", "Remote plasma", 16, 8, 150));
    pieces.push({ id: "plasma", kind: "plasma", x: 20, y: 48, w: 56, h: 56 });
  } else {
    pieces.push(textPiece("chamber-name", "Chamber", 16, 8, 88));
  }
  if (options.showerhead) {
    pieces.push(textPiece("showerhead", "Showerhead", 220, 8, 140));
    pieces.push({ id: "showerhead-line", kind: "showerhead", x: 220, y: 28, w: 280, h: 8 });
  }
  const upper = present(map, ["chamber.wall", "chamber.pressure", "chuck.esc", "chuck.heater", "chuck.temperature", "chuck.edgeRing"]);
  if (upper.length > 0) pieces.push(groupRect("chamber", upper));
  if (options.heater && map["chuck.heater"]) pieces.push(beside(map["chuck.heater"], "heater", "Heater"));
  const gases = present(map, Object.keys(map).filter((key) => key.startsWith("gas.")));
  if (gases.length > 0) {
    pieces.push(beside(gases[0], "gas-box", "Gas box"));
    pieces.push({ id: "gas-rail", kind: "line", x: 4, y: gases[0].y - 2, w: 4, h: 24 });
  }
  const matchAt = map["rf.source.forward"] ?? map["rf.forward"] ?? map["rf.remote.forward"];
  if (matchAt) {
    pieces.push({ id: "match", kind: "match", x: 16, y: matchAt.y - 48, w: 40, h: 14 });
    pieces.push(beside(matchAt, "match-label", "Match", -22));
    if (map["rf.source.forward"] || map["rf.remote.forward"]) pieces.push(beside(matchAt, "source", "Source", 4));
  }
  if (map["rf.bias.forward"]) pieces.push(beside(map["rf.bias.forward"], "bias", "Bias"));
  const vibration = map["vacuum.vibration"];
  if (vibration) {
    const throttleY = vibration.y - BOX_H / 2 - 34;
    pieces.push({ id: "throttle", kind: "throttle", x: 16, y: throttleY, w: 28, h: 28 });
    pieces.push(textPiece("throttle-label", "Throttle", 48, throttleY + 5, 96));
    pieces.push({ id: "pump", kind: "pump", x: 16, y: vibration.y - 18, w: 36, h: 36 });
    pieces.push(textPiece("pump-label", "Pump", 58, vibration.y - 9, 64));
  }
  return pieces;
}

function handlerArt(boxes: AnchorBox[]): ArtPiece[] {
  const shift = boxes.length === 0 ? 190 : 0;
  return [
    textPiece("load-lock", "Load lock", 20 + shift, 8, 110),
    { id: "cassette", kind: "rect", x: 24 + shift, y: 36, w: 150, h: 140 },
    textPiece("cassette-label", "Cassette", 36 + shift, 48, 100),
    ...Array.from({ length: 4 }, (_, index) => ({
      id: `wafer-${index}`,
      kind: "line" as const,
      x: 40 + shift,
      y: 80 + index * 18,
      w: 100,
      h: 4,
    })),
    textPiece("handler-label", "Wafer handler", 20 + shift, 188, 150),
    { id: "handler-path", kind: "line", x: 174 + shift, y: 100, w: boxes.length === 0 ? 80 : 140, h: 4 },
  ];
}

function bathArt(map: Record<string, AnchorBox>): ArtPiece[] {
  const pieces: ArtPiece[] = [
    textPiece("spray", "Spray bar", 16, 8, 110),
    textPiece("bath-label", "Bath", 220, 8, 72),
    { id: "spray-line", kind: "line", x: 300, y: 14, w: 180, h: 4 },
  ];
  const tub = present(map, ["chamber.pressure", "chuck.bath", "chuck.megasonic", "chamber.particles", "chamber.residue"]);
  if (tub.length > 0) pieces.push(groupRect("bath", tub));
  if (map["chuck.bath"]) {
    pieces.push({
      id: "spin",
      kind: "ellipse",
      x: map["chuck.bath"].x - 70,
      y: map["chuck.bath"].y + 28,
      w: 140,
      h: 16,
    });
  }
  if (map["handler.runout"]) pieces.push(beside(map["handler.runout"], "spin-label", "Spin chuck", -20));
  if (map["chuck.megasonic"]) {
    pieces.push(beside(map["chuck.megasonic"], "mega-label", "Megasonic", 16));
    pieces.push({
      id: "mega",
      kind: "megasonic",
      x: map["chuck.megasonic"].x - 60,
      y: map["chuck.megasonic"].y + 30,
      w: 120,
      h: 12,
    });
  }
  if (map["chamber.pressure"]) {
    pieces.push({ id: "gauge", kind: "gauge", x: 20, y: map["chamber.pressure"].y - 16, w: 32, h: 32 });
  }
  return pieces;
}

function manifoldArt(map: Record<string, AnchorBox>): ArtPiece[] {
  const pieces: ArtPiece[] = [textPiece("manifold", "Chemical manifold", 16, 8, 180)];
  const gases = present(map, ["gas.sc1", "gas.sc2", "gas.diw"]);
  if (gases.length > 0) {
    const top = gases[0].y - 10;
    const bottom = gases[gases.length - 1].y + 10;
    pieces.push({ id: "manifold-line", kind: "line", x: 28, y: top, w: 4, h: bottom - top });
  }
  const vibration = map["vacuum.vibration"];
  if (vibration) {
    pieces.push({ id: "pump", kind: "pump", x: 16, y: vibration.y - 20, w: 36, h: 36 });
    pieces.push(textPiece("drain", "Drain pump", 58, vibration.y - 9, 110));
  }
  return pieces;
}

export function artPieces(panel: SchematicPanel): ArtPiece[] {
  const map = byBody(panel.boxes);
  switch (panel.art) {
    case "etch":
      return flowArt(map, { showerhead: true, plasma: false, heater: false });
    case "deposition":
      return flowArt(map, { showerhead: true, plasma: false, heater: true });
    case "strip":
      return flowArt(map, { showerhead: false, plasma: true, heater: false });
    case "handler":
      return handlerArt(panel.boxes);
    case "cleanBath":
      return bathArt(map);
    case "cleanManifold":
      return manifoldArt(map);
    default:
      return [];
  }
}

function overlaps(a: Rect, b: Rect): boolean {
  return a.x < b.x + b.w - 0.5 && a.x + a.w > b.x + 0.5 && a.y < b.y + b.h - 0.5 && a.y + a.h > b.y + 0.5;
}

function boxRect(box: AnchorBox): Rect {
  return { x: box.x - box.w / 2, y: box.y - box.h / 2, w: box.w, h: box.h };
}

export function labelProblems(panel: SchematicPanel): string[] {
  const pieces = artPieces(panel);
  const problems: string[] = [];
  for (const piece of pieces) {
    if (piece.x < 0 || piece.y < 0 || piece.x + piece.w > panel.view.width || piece.y + piece.h > panel.view.height) {
      problems.push(`${piece.id} outside`);
    }
  }
  const labels = pieces.filter((piece) => piece.label);
  for (const piece of labels) {
    if ((piece.label?.length ?? 0) * 7.6 > piece.w) problems.push(`${piece.id} narrow`);
    if (piece.h < 16) problems.push(`${piece.id} short`);
    for (const box of panel.boxes) {
      if (overlaps(piece, boxRect(box))) problems.push(`${piece.id} hits ${box.anchor}`);
    }
    for (const other of labels) {
      if (other.id <= piece.id) continue;
      if (overlaps(piece, other)) problems.push(`${piece.id} hits ${other.id}`);
    }
  }
  return problems;
}

const MONO_EM = 0.66;
const SANS_EM = 0.58;
const CHROME_X = 14;
const CHROME_Y = 6;
const LINE = 1.25;
const DOT_PX = 12;

function wrapCount(text: string, inner: number, fontPx: number, em: number, firstInner = inner): number {
  if (text.length === 0) return 1;
  const raw = text.split(/(\s+|-)/).filter((part) => part.length > 0);
  const chunks: string[] = [];
  for (const part of raw) {
    if (part === "-" && chunks.length > 0) chunks[chunks.length - 1] += "-";
    else chunks.push(part);
  }
  let lines = 1;
  let used = 0;
  let limit = firstInner;
  for (const chunk of chunks) {
    if (/^\s+$/.test(chunk)) {
      const width = chunk.length * em * fontPx;
      if (used > 0 && used + width <= limit) used += width;
      continue;
    }
    const width = chunk.length * em * fontPx;
    if (width > inner) return 99;
    if (used > 0 && used + width > limit) {
      lines += 1;
      used = width;
      limit = inner;
    } else {
      used += width;
    }
  }
  return lines;
}

export function faceplateProblems(panel: SchematicPanel, panelPx: number, textFor: (anchor: string) => FaceplateText): string[] {
  const problems: string[] = [];
  for (const box of panel.boxes) {
    const text = textFor(box.anchor);
    const width = panel.kind === "counters" ? panelPx : (box.w / panel.view.width) * panelPx;
    const height = panel.kind === "counters" ? Number.POSITIVE_INFINITY : (box.h / panel.view.width) * panelPx;
    const inner = width - CHROME_X;
    const labelLines = wrapCount(text.label, inner, SENSOR_FONT_PX, SANS_EM, inner - DOT_PX);
    const valueLines = wrapCount(text.value, inner, VALUE_FONT_PX, MONO_EM);
    const idLines = wrapCount(text.id, inner, SENSOR_FONT_PX, MONO_EM);
    if (labelLines > 2 || valueLines > 2 || idLines > 2) {
      problems.push(box.anchor);
      continue;
    }
    const needed =
      labelLines * SENSOR_FONT_PX * LINE + valueLines * VALUE_FONT_PX * LINE + idLines * SENSOR_FONT_PX * LINE + CHROME_Y;
    if (needed > height) problems.push(box.anchor);
  }
  return problems;
}
