// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company
// Physical fab floor, plan view. Not the console tool schematic, and not the Azure sheet.

import { CREAM, INK, MUTED, NAVY, TONES, WHITE } from './sheet.mjs';

const SHELL = '#E8E2D4';

function paint(sheet, x, y, w, h, rx, attrs) {
  sheet.roundRect(x, y, w, h, rx, attrs);
}

function circle(sheet, cx, cy, r, attrs) {
  const parts = Object.entries(attrs)
    .filter(([, v]) => v !== undefined && v !== null)
    .map(([k, v]) => `${k}="${v}"`)
    .join(' ');
  sheet.shape(`<circle cx="${cx.toFixed(2)}" cy="${cy.toFixed(2)}" r="${r}" ${parts}/>`);
}

function bar(sheet, x, y, h, fill) {
  sheet.shape(
    `<rect x="${(x + 1).toFixed(2)}" y="${(y + 8).toFixed(2)}" width="7" height="${(h - 16).toFixed(2)}" rx="2" fill="${fill}"/>`,
  );
}

// Installed tool. Load ports face the rail. Chambers sit on the far side.
function drawInstalled(sheet, x, y, w, h, spec, clip) {
  const tone = TONES[spec.tone];
  paint(sheet, x, y, w, h, 6, { fill: WHITE, stroke: tone.stroke, 'stroke-width': 1.8 });
  bar(sheet, x, y, h, tone.bar);

  const idH = 40;
  const portH = 30;
  const south = spec.ports === 'south';
  const idBaseline = south ? y + 26 : y + h - 14;
  sheet.text(spec.id, spec.id, x + w / 2, idBaseline, {
    size: 16,
    weight: 'semibold',
    fill: INK,
    align: 'center',
    clip,
  });

  const body = south
    ? { x: x + 14, y: y + idH + 10, w: w - 36, h: h - idH - portH - 28 }
    : { x: x + 14, y: y + portH + 18, w: w - 36, h: h - idH - portH - 28 };
  paint(sheet, body.x, body.y, body.w, body.h, 4, {
    fill: '#FBFBF8',
    stroke: tone.stroke,
    'stroke-width': 1.4,
  });
  sheet.occupy(`${spec.id}-body`, body.x, body.y, body.w, body.h);

  const r = 20;
  const cy = south ? body.y + r + 8 : body.y + body.h - r - 8;
  const centers = spec.chambers === 1 ? [body.x + body.w / 2] : [body.x + body.w * 0.32, body.x + body.w * 0.68];
  centers.forEach((cx, i) => {
    circle(sheet, cx, cy, r, { fill: tone.zone, stroke: tone.stroke, 'stroke-width': 1.6 });
    sheet.occupy(`${spec.id}-ch${i}`, cx - r, cy - r, r * 2, r * 2);
    if (spec.ring) {
      circle(sheet, cx, cy, 10, { fill: 'none', stroke: tone.stroke, 'stroke-width': 1.3 });
    }
  });

  const nub = { x: body.x + body.w + 4, y: body.y + body.h / 2 - 22, w: 14, h: 44 };
  paint(sheet, nub.x, nub.y, nub.w, nub.h, 2, { fill: tone.zone, stroke: tone.stroke, 'stroke-width': 1.2 });
  sheet.occupy(`${spec.id}-nub`, nub.x, nub.y, nub.w, nub.h);

  const n = spec.chambers === 1 ? 1 : 2;
  const portW = 46;
  const gap = 14;
  const total = portW * n + gap * (n - 1);
  let px = body.x + (body.w - total) / 2;
  const py = south ? y + h - 8 - portH : y + 8;
  for (let i = 0; i < n; i += 1) {
    paint(sheet, px, py, portW, portH, 2, { fill: CREAM, stroke: tone.stroke, 'stroke-width': 1.3 });
    sheet.occupy(`${spec.id}-port${i}`, px, py, portW, portH);
    px += portW + gap;
  }
}

// Enlarged plan of one tool type. Labels name the load ports and the chambers.
function drawType(sheet, box, spec) {
  const tone = TONES[spec.tone];
  const clip = { x: box.x + 2, y: box.y + 2, w: box.w - 4, h: box.h - 4 };
  paint(sheet, box.x, box.y, box.w, box.h, 8, { fill: WHITE, stroke: tone.stroke, 'stroke-width': 1.8 });
  bar(sheet, box.x, box.y, box.h, tone.bar);

  sheet.text(`${spec.key}-title`, spec.title, box.x + 20, box.y + 24, {
    size: 16,
    weight: 'semibold',
    fill: tone.title,
    clip,
  });
  sheet.text(`${spec.key}-sub`, spec.subtitle, box.x + 20, box.y + 50, {
    size: 14,
    weight: 'regular',
    fill: MUTED,
    clip,
  });

  const body = { x: box.x + 196, y: box.y + 112, w: 340, h: 96 };
  paint(sheet, body.x, body.y, body.w, body.h, 4, {
    fill: '#FBFBF8',
    stroke: tone.stroke,
    'stroke-width': 1.6,
  });
  sheet.occupy(`${spec.key}-body`, body.x, body.y, body.w, body.h);

  const r = 26;
  const cy = body.y + 4;
  const centers = spec.chambers === 1 ? [body.x + body.w / 2] : [body.x + 86, body.x + body.w - 86];
  const names = spec.chambers === 1 ? ['Chamber'] : ['Chamber A', 'Chamber B'];
  centers.forEach((cx, i) => {
    circle(sheet, cx, cy, r, { fill: tone.zone, stroke: tone.stroke, 'stroke-width': 1.8 });
    sheet.occupy(`${spec.key}-ch${i}`, cx - r, cy - r, r * 2, r * 2);
    if (spec.ring) circle(sheet, cx, cy, 12, { fill: 'none', stroke: tone.stroke, 'stroke-width': 1.4 });
    sheet.text(`${spec.key}-cl${i}`, names[i], cx, box.y + 76, {
      size: 14,
      weight: 'semibold',
      fill: INK,
      align: 'center',
      clip,
    });
  });

  sheet.text(`${spec.key}-handler`, 'Wafer handler', box.x + 22, body.y + body.h / 2 + 4, {
    size: 14,
    weight: 'regular',
    fill: INK,
    clip,
  });

  const side = { x: body.x + body.w + 18, y: body.y + 16, w: 58, h: 64 };
  paint(sheet, side.x, side.y, side.w, side.h, 3, { fill: tone.zone, stroke: tone.stroke, 'stroke-width': 1.4 });
  sheet.occupy(`${spec.key}-side`, side.x, side.y, side.w, side.h);
  sheet.text(`${spec.key}-side-label`, spec.side, side.x + side.w + 12, side.y + 36, {
    size: 14,
    weight: 'regular',
    fill: INK,
    clip,
  });

  const n = spec.chambers === 1 ? 1 : 2;
  const portW = 64;
  const gap = 28;
  const total = portW * n + gap * (n - 1);
  let px = body.x + (body.w - total) / 2;
  const py = body.y + body.h + 14;
  for (let i = 0; i < n; i += 1) {
    paint(sheet, px, py, portW, 28, 2, { fill: CREAM, stroke: tone.stroke, 'stroke-width': 1.4 });
    sheet.occupy(`${spec.key}-port${i}`, px, py, portW, 28);
    px += portW + gap;
  }
  sheet.text(`${spec.key}-ports`, spec.portLabel, body.x + body.w / 2, py + 52, {
    size: 14,
    weight: 'semibold',
    fill: INK,
    align: 'center',
    clip,
  });
}

function roomLabel(sheet, id, lines, x, baselines, clip) {
  lines.forEach((line, i) => {
    sheet.text(`${id}-${i}`, line.text, x, baselines[i], {
      size: line.size || 14,
      weight: line.weight || 'regular',
      fill: line.fill || INK,
      clip,
    });
  });
}

const FAMILY = {
  D: { word: 'deposition', tone: 'det', chambers: 2, ring: false },
  E: { word: 'etch', tone: 'review', chambers: 2, ring: true },
  S: { word: 'strip', tone: 'ai', chambers: 2, ring: false },
  C: { word: 'clean', tone: 'ok', chambers: 1, ring: false },
};
const FAMILY_ORDER = ['D', 'E', 'S', 'C'];

/** Family letter of a tool id: D-01 is D, I-E-01 is E, P-S-02 is S. */
export function familyOf(toolId) {
  const parts = toolId.split('-');
  return parts[parts.length - 2];
}

/** "Deposition, etch, strip, clean": the same caption the console shows under a bay name. */
export function bayCaption(toolIds) {
  const letters = FAMILY_ORDER.filter((f) => toolIds.some((id) => familyOf(id) === f));
  const text = letters.map((f) => FAMILY[f].word).join(', ');
  return text.charAt(0).toUpperCase() + text.slice(1);
}

function drawBay(sheet, room, bay, top, tone) {
  paint(sheet, room.x, room.y, room.w, room.h, 6, { fill: tone.zone, stroke: tone.stroke, 'stroke-width': 1.6 });
  const ids = bay.tools.map((t) => t.id);
  const lines = [
    { text: bay.name, size: 16, weight: 'semibold', fill: tone.title },
    { text: `${bayCaption(ids)}. ${ids.join(', ')}.` },
  ];
  const baselines = top ? [room.y + 26, room.y + 52] : [room.y + room.h - 50, room.y + room.h - 24];
  roomLabel(sheet, `bay-${bay.id}`, lines, room.x + 16, baselines, room);

  const slots = Math.max(ids.length, 3);
  const inset = 12;
  const gap = 12;
  const toolW = (room.w - inset * 2 - gap * (slots - 1)) / slots;
  const toolH = room.h - 84;
  const used = toolW * ids.length + gap * (ids.length - 1);
  let x = room.x + (room.w - used) / 2;
  const y = top ? room.y + room.h - 10 - toolH : room.y + 10;
  for (const id of ids) {
    const fam = FAMILY[familyOf(id)];
    drawInstalled(sheet, x, y, toolW, toolH, {
      id,
      tone: fam.tone,
      ports: top ? 'south' : 'north',
      chambers: fam.chambers,
      ring: fam.ring,
    }, room);
    x += toolW + gap;
  }
}

/** One plan-view floor for a site in data/fleet/sites.yaml: its real bays, bay names and tool ids. */
export function drawSiteFloor(site) {
  return (sheet) => {
    const a = sheet.area;
    const banner = { x: a.x, y: a.y + 6, w: a.w, h: 44 };
    paint(sheet, banner.x, banner.y, banner.w, banner.h, 8, { fill: '#FFF8E6', stroke: '#E6D39A', 'stroke-width': 1 });
    sheet.shape(
      `<rect x="${banner.x.toFixed(2)}" y="${banner.y.toFixed(2)}" width="7" height="${banner.h.toFixed(2)}" rx="3" fill="#FAC400"/>`,
    );
    sheet.text(
      'banner',
      `Illustrative. ${site.name}, ${site.location}. ${site.role}. Not a survey and not a real building.`,
      banner.x + 20,
      banner.y + 28,
      { size: 16, weight: 'semibold', fill: NAVY, clip: banner },
    );

    const detailW = 908;
    const gutter = 14;
    const plan = { x: a.x, y: banner.y + banner.h + 10, w: a.w - detailW - gutter, h: a.y + a.h - (banner.y + banner.h + 10) - 6 };
    const detail = { x: plan.x + plan.w + gutter, y: plan.y, w: detailW, h: plan.h };
    paint(sheet, plan.x, plan.y, plan.w, plan.h, 8, { fill: SHELL, stroke: NAVY, 'stroke-width': 2 });

    const inner = { x: plan.x + 10, y: plan.y + 10, w: plan.w - 20, h: plan.h - 20 };
    const northH = 84;
    const southH = 168;
    const sideW = 120;
    const gap = 8;
    const north = { x: inner.x, y: inner.y, w: inner.w, h: northH };
    const south = { x: inner.x, y: inner.y + inner.h - southH, w: inner.w, h: southH };
    const midY = north.y + north.h + gap;
    const midH = south.y - gap - midY;
    const spineH = 150;
    const bayH = (midH - spineH - gap * 2) / 2;
    const bayX = inner.x + sideW + gap;
    const bayW = inner.w - sideW * 2 - gap * 2;
    const halfW = (bayW - gap) / 2;
    const spine = { x: bayX, y: midY + bayH + gap, w: bayW, h: spineH };
    const west = { x: inner.x, y: midY, w: sideW, h: midH };
    const east = { x: inner.x + inner.w - sideW, y: midY, w: sideW, h: midH };

    const chaseTone = TONES.gov;
    for (const room of [north, south, west, east]) {
      paint(sheet, room.x, room.y, room.w, room.h, 6, { fill: chaseTone.zone, stroke: chaseTone.stroke, 'stroke-width': 1.4 });
    }
    roomLabel(sheet, 'north', [
      { text: 'North utility chase', size: 16, weight: 'semibold', fill: chaseTone.title },
      { text: 'Gases, exhaust, and power. Pumps, chillers, and abatement are in the sub-fab below.' },
    ], north.x + 16, [north.y + 30, north.y + 58], north);
    sheet.text('north-up', 'North is up', north.x + north.w - 16, north.y + 30, {
      size: 14, weight: 'semibold', fill: chaseTone.title, align: 'right', clip: north,
    });
    const flows = (site.flows || []).slice(0, 4).map((f) => ({ text: `Wafer flow: ${f.label}` }));
    roomLabel(sheet, 'south', [
      { text: 'South utility chase', size: 16, weight: 'semibold', fill: chaseTone.title },
      ...flows,
    ], south.x + 16, [0, 1, 2, 3, 4].map((i) => south.y + 28 + i * 30), south);
    for (const [room, id, label] of [[west, 'west', 'West chase'], [east, 'east', 'East chase']]) {
      const mid = room.y + room.h / 2;
      sheet.text(`${id}-0`, label, room.x + room.w / 2, mid - 12, { size: 16, weight: 'semibold', fill: chaseTone.title, align: 'center', clip: room });
      sheet.text(`${id}-1`, 'Utility', room.x + room.w / 2, mid + 16, { size: 14, weight: 'regular', fill: INK, align: 'center', clip: room });
    }

    const rooms = [
      { x: bayX, y: midY, w: halfW, h: bayH, top: true },
      { x: bayX + halfW + gap, y: midY, w: halfW, h: bayH, top: true },
      { x: bayX, y: spine.y + spine.h + gap, w: halfW, h: bayH, top: false },
      { x: bayX + halfW + gap, y: spine.y + spine.h + gap, w: halfW, h: bayH, top: false },
    ];
    site.bays.slice(0, 4).forEach((bay, i) => {
      const pilot = /pilot/i.test(bay.name);
      drawBay(sheet, rooms[i], bay, rooms[i].top, pilot ? TONES.risk : TONES.human);
    });

    paint(sheet, spine.x, spine.y, spine.w, spine.h, 6, { fill: WHITE, stroke: NAVY, 'stroke-width': 1.4 });
    const stockW = 148;
    const stockH = 104;
    const stockY = spine.y + (spine.h - stockH) / 2;
    const westStock = { x: spine.x + 10, y: stockY, w: stockW, h: stockH };
    const eastStock = { x: spine.x + spine.w - 10 - stockW, y: stockY, w: stockW, h: stockH };
    const loop = { x: westStock.x + westStock.w, y: spine.y + (spine.h - 78) / 2, w: eastStock.x - (westStock.x + westStock.w), h: 78 };
    sheet.shape(
      `<rect x="${loop.x.toFixed(2)}" y="${loop.y.toFixed(2)}" width="${loop.w.toFixed(2)}" height="${loop.h.toFixed(2)}" rx="18" fill="none" stroke="${NAVY}" stroke-width="8"/>`,
    );
    const sw = 8;
    sheet.occupy('rail-n', loop.x + 16, loop.y - sw / 2, loop.w - 32, sw);
    sheet.occupy('rail-s', loop.x + 16, loop.y + loop.h - sw / 2, loop.w - 32, sw);
    const stockTone = TONES.gw;
    for (const [stock, id] of [[westStock, 'west'], [eastStock, 'east']]) {
      paint(sheet, stock.x, stock.y, stock.w, stock.h, 6, { fill: stockTone.zone, stroke: stockTone.stroke, 'stroke-width': 1.6 });
      sheet.text(`stock-${id}-a`, 'Stocker', stock.x + stock.w / 2, stock.y + 46, { size: 16, weight: 'semibold', fill: stockTone.title, align: 'center', clip: stock });
      sheet.text(`stock-${id}-b`, 'Wafer carriers', stock.x + stock.w / 2, stock.y + 72, { size: 14, weight: 'regular', fill: INK, align: 'center', clip: stock });
    }
    const hole = { x: loop.x + 18, y: loop.y + 16, w: loop.w - 36, h: loop.h - 32 };
    sheet.text('rail-label', 'Overhead wafer rail', hole.x + hole.w / 2, hole.y + hole.h / 2 + 5, { size: 16, weight: 'semibold', fill: NAVY, align: 'center', clip: hole });

    const present = FAMILY_ORDER.filter((f) => site.bays.some((b) => b.tools.some((t) => familyOf(t.id) === f)));
    const types = TYPES.filter((t) => present.includes(t.letter));
    const panelGap = 12;
    const panelH = (detail.h - panelGap * (types.length - 1)) / types.length;
    types.forEach((spec, i) => drawType(sheet, { x: detail.x, y: detail.y + i * (panelH + panelGap), w: detail.w, h: panelH }, spec));
  };
}

const TYPES = [
  { letter: 'D', key: 'dep', title: 'Deposition', subtitle: 'MPS-D7. Plan view. Two chambers. Thin film.', tone: 'det', chambers: 2, side: 'Gas box', portLabel: 'Load ports' },
  { letter: 'E', key: 'etch', title: 'Etch', subtitle: 'MPS-E7. Plan view. The inner ring is the edge ring.', tone: 'review', chambers: 2, ring: true, side: 'Gas box', portLabel: 'Load ports' },
  { letter: 'S', key: 'strip', title: 'Strip', subtitle: 'MPS-S7. Plan view. Two chambers. Photoresist strip.', tone: 'ai', chambers: 2, side: 'Gas box', portLabel: 'Load ports' },
  { letter: 'C', key: 'clean', title: 'Clean', subtitle: 'MPS-C7. Plan view. One chamber. Single wafer.', tone: 'ok', chambers: 1, side: 'Chemical rack', portLabel: 'Load port' },
];

/** One print sheet per site, in sites.yaml order. */
export function floorSheets(sites) {
  return sites.map((site, i) => ({
    sheetId: `G7${String.fromCharCode(97 + i)}`,
    issue: 'G7',
    kicker: 'FAB FLOOR PLAN',
    title: `${site.name} floor plan`,
    slug: `fab-floor-${site.id}`,
    strictLabels: true,
    draw: drawSiteFloor(site),
    legend: [
      { tone: 'human', label: 'Cleanroom bay. Filtered air.' },
      { tone: 'risk', label: 'Pilot line bay.' },
      { tone: 'gov', label: 'Utility chase. Sub-fab is below.' },
      { tone: 'gw', label: 'Overhead rail and stocker.' },
      { tone: 'det', label: 'Deposition tool. MPS-D7.' },
      { tone: 'review', label: 'Etch tool. MPS-E7.' },
      { tone: 'ai', label: 'Strip tool. MPS-S7.' },
      { tone: 'ok', label: 'Clean tool. MPS-C7.' },
    ],
  }));
}
