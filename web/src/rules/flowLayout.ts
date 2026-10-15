// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company
// Process-map boxes are laid out in a 1280-wide design space and scale uniformly.

export type FlowBox = {
  id: string;
  text: string;
  x: number;
  y: number;
  w: number;
  h: number;
  fontSize: number;
};

export type FlowLayout = {
  width: number;
  height: number;
  boxes: FlowBox[];
};

export type FlowStep = {
  id: string;
  name: string;
  detail?: string | null;
};

const FONT = 16;
const BOX_W = 560;
const GAP_X = 48;
const GAP_Y = 28;
const MARGIN = 24;
const PAD_X = 16;
const PAD_Y = 12;
const COLS = 2;

export function textWidth(text: string, fontSize: number): number {
  return text.length * fontSize * 0.62;
}

export function labelFits(box: FlowBox): boolean {
  const lines = box.text.split("\n");
  const lineHeight = box.fontSize * 1.35;
  const needed = PAD_Y * 2 + lines.length * lineHeight;
  if (needed > box.h + 0.01) return false;
  const inner = box.w - PAD_X * 2;
  return lines.every((line) => textWidth(line, box.fontSize) <= inner + 0.01);
}

export function labelInside(box: FlowBox, width: number, height: number, margin = 8): boolean {
  return (
    box.x >= margin - 0.01 &&
    box.y >= margin - 0.01 &&
    box.x + box.w <= width - margin + 0.01 &&
    box.y + box.h <= height - margin + 0.01
  );
}

export function overlappingPairs(boxes: FlowBox[], gap = 2): string[] {
  const hits: string[] = [];
  for (let i = 0; i < boxes.length; i += 1) {
    for (let j = i + 1; j < boxes.length; j += 1) {
      const a = boxes[i];
      const b = boxes[j];
      const hit = a.x < b.x + b.w + gap && b.x < a.x + a.w + gap && a.y < b.y + b.h + gap && b.y < a.y + a.h + gap;
      if (hit) hits.push(`${a.id} x ${b.id}`);
    }
  }
  return hits;
}

function wrapLine(text: string, innerWidth: number, fontSize: number): string[] {
  const words = text.split(/\s+/).filter((word) => word.length > 0);
  const lines: string[] = [];
  let current = "";
  for (const word of words) {
    const next = current ? `${current} ${word}` : word;
    if (textWidth(next, fontSize) <= innerWidth) current = next;
    else {
      if (current) lines.push(current);
      current = word;
    }
  }
  if (current) lines.push(current);
  return lines.length > 0 ? lines : [""];
}

export function layoutFlow(steps: FlowStep[], designWidth = 1280): FlowLayout {
  const inner = BOX_W - PAD_X * 2;
  const prepared = steps.map((step) => {
    const parts = [step.name];
    if (step.detail) parts.push(step.detail);
    const lines = parts.flatMap((part) => wrapLine(part, inner, FONT));
    const text = lines.join("\n");
    const h = PAD_Y * 2 + lines.length * FONT * 1.35 + 4;
    return { id: step.id, text, h };
  });
  const rows = Math.max(1, Math.ceil(prepared.length / COLS));
  const rowHeights: number[] = [];
  for (let row = 0; row < rows; row += 1) {
    const slice = prepared.slice(row * COLS, row * COLS + COLS);
    rowHeights.push(Math.max(...slice.map((item) => item.h), FONT));
  }
  const boxes: FlowBox[] = prepared.map((item, index) => {
    const col = index % COLS;
    const row = Math.floor(index / COLS);
    const y = MARGIN + rowHeights.slice(0, row).reduce((sum, height) => sum + height + GAP_Y, 0);
    return {
      id: item.id,
      text: item.text,
      x: MARGIN + col * (BOX_W + GAP_X),
      y,
      w: BOX_W,
      h: rowHeights[row],
      fontSize: FONT,
    };
  });
  const contentWidth = MARGIN * 2 + COLS * BOX_W + (COLS - 1) * GAP_X;
  const height =
    MARGIN + rowHeights.reduce((sum, height) => sum + height, 0) + Math.max(0, rows - 1) * GAP_Y + MARGIN;
  return { width: Math.max(designWidth, contentWidth), height, boxes };
}

export function scaleFlow(layout: FlowLayout, viewportWidth: number): FlowLayout {
  const scale = viewportWidth / layout.width;
  return {
    width: layout.width * scale,
    height: layout.height * scale,
    boxes: layout.boxes.map((box) => ({
      ...box,
      x: box.x * scale,
      y: box.y * scale,
      w: box.w * scale,
      h: box.h * scale,
      fontSize: box.fontSize * scale,
    })),
  };
}
