// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company
import type { ToolFamily } from "../api/types";

export const SCHEMATIC_VIEW = { width: 1600, height: 1000 } as const;

export type AnchorBox = {
  anchor: string;
  x: number;
  y: number;
  w: number;
  h: number;
};

export const CHAMBER_SHIFT = 660;

type Point = { x: number; y: number; w: number; h: number };

const TOOL: Point[] = [
  { x: 250, y: 940, w: 280, h: 52 },
  { x: 610, y: 940, w: 280, h: 52 },
  { x: 980, y: 940, w: 280, h: 52 },
  { x: 1350, y: 940, w: 280, h: 52 },
];

const TOOL_ANCHORS = ["tool.runRate", "tool.throughput", "tool.output", "tool.yield"] as const;

const HANDLER: AnchorBox[] = [
  { anchor: "a.handler.center", x: 128, y: 390, w: 210, h: 52 },
  { anchor: "b.handler.center", x: 128, y: 470, w: 210, h: 52 },
];

const ETCH: Record<string, Point> = {
  "chamber.wall": { x: 770, y: 96, w: 168, h: 52 },
  "chamber.pressure": { x: 430, y: 180, w: 168, h: 52 },
  "chuck.esc": { x: 690, y: 180, w: 168, h: 52 },
  "chuck.edgeRing": { x: 690, y: 258, w: 168, h: 52 },
  "chamber.etchRate": { x: 410, y: 360, w: 168, h: 52 },
  "chamber.endpoint": { x: 600, y: 360, w: 168, h: 52 },
  "chamber.particles": { x: 790, y: 360, w: 160, h: 52 },
  "gas.cf4": { x: 376, y: 500, w: 136, h: 52 },
  "gas.chf3": { x: 530, y: 500, w: 136, h: 52 },
  "gas.o2": { x: 684, y: 500, w: 136, h: 52 },
  "gas.ar": { x: 838, y: 500, w: 136, h: 52 },
  "rf.source.forward": { x: 450, y: 640, w: 200, h: 52 },
  "rf.source.reflected": { x: 760, y: 640, w: 200, h: 52 },
  "rf.bias.forward": { x: 450, y: 730, w: 200, h: 52 },
  "rf.bias.reflected": { x: 760, y: 730, w: 200, h: 52 },
  "vacuum.vibration": { x: 600, y: 830, w: 180, h: 52 },
};

const DEPOSITION: Record<string, Point> = {
  "chamber.pressure": { x: 440, y: 170, w: 168, h: 52 },
  "chuck.heater": { x: 700, y: 190, w: 180, h: 52 },
  "chamber.thickness": { x: 400, y: 340, w: 168, h: 52 },
  "chamber.uniformity": { x: 610, y: 340, w: 180, h: 52 },
  "chamber.particles": { x: 820, y: 340, w: 168, h: 52 },
  "gas.sih4": { x: 420, y: 500, w: 170, h: 52 },
  "gas.n2o": { x: 620, y: 500, w: 170, h: 52 },
  "gas.nh3": { x: 820, y: 500, w: 170, h: 52 },
  "rf.forward": { x: 480, y: 660, w: 200, h: 52 },
  "rf.reflected": { x: 760, y: 660, w: 200, h: 52 },
  "vacuum.vibration": { x: 620, y: 820, w: 180, h: 52 },
};

const STRIP: Record<string, Point> = {
  "chamber.pressure": { x: 460, y: 160, w: 180, h: 52 },
  "chuck.temperature": { x: 730, y: 210, w: 190, h: 52 },
  "chamber.ashRate": { x: 480, y: 360, w: 190, h: 52 },
  "chamber.particles": { x: 760, y: 360, w: 180, h: 52 },
  "gas.o2": { x: 480, y: 520, w: 200, h: 52 },
  "gas.n2h2": { x: 760, y: 520, w: 220, h: 52 },
  "rf.remote.forward": { x: 480, y: 680, w: 220, h: 52 },
  "rf.remote.reflected": { x: 780, y: 680, w: 230, h: 52 },
  "vacuum.vibration": { x: 620, y: 820, w: 180, h: 52 },
};

const CLEAN: AnchorBox[] = [
  { anchor: "a.chamber.pressure", x: 460, y: 150, w: 180, h: 52 },
  { anchor: "a.chuck.bath", x: 720, y: 230, w: 180, h: 52 },
  { anchor: "a.handler.runout", x: 980, y: 260, w: 200, h: 52 },
  { anchor: "a.chuck.megasonic", x: 720, y: 340, w: 180, h: 52 },
  { anchor: "a.chamber.particles", x: 460, y: 450, w: 180, h: 52 },
  { anchor: "a.chamber.residue", x: 720, y: 450, w: 180, h: 52 },
  { anchor: "a.gas.sc1", x: 1320, y: 150, w: 220, h: 52 },
  { anchor: "a.gas.sc2", x: 1320, y: 250, w: 220, h: 52 },
  { anchor: "a.gas.diw", x: 1320, y: 350, w: 220, h: 52 },
  { anchor: "a.vacuum.vibration", x: 1320, y: 520, w: 220, h: 52 },
];

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

function dual(points: Record<string, Point>): AnchorBox[] {
  const boxes: AnchorBox[] = [];
  for (const [body, point] of Object.entries(points)) {
    boxes.push({ anchor: `a.${body}`, ...point });
    boxes.push({ anchor: `b.${body}`, ...point, x: point.x + CHAMBER_SHIFT });
  }
  return boxes;
}

function toolBoxes(): AnchorBox[] {
  return TOOL_ANCHORS.map((anchor, index) => ({ anchor, ...TOOL[index] }));
}

const BY_FAMILY: Record<ToolFamily, AnchorBox[]> = {
  etch: [...HANDLER, ...dual(ETCH), ...toolBoxes()],
  deposition: [...HANDLER, ...dual(DEPOSITION), ...toolBoxes()],
  strip: [...HANDLER, ...dual(STRIP), ...toolBoxes()],
  clean: [...CLEAN, ...toolBoxes()],
};

export function anchorBoxes(family: ToolFamily): AnchorBox[] {
  return BY_FAMILY[family];
}

export function anchorById(family: ToolFamily): Record<string, AnchorBox> {
  return Object.fromEntries(anchorBoxes(family).map((box) => [box.anchor, box]));
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
