// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company
// Layout engine for ARCH D print sheets. Coordinates are points (1/72 in).

import fs from 'node:fs';

export const PAGE_W = 36 * 72;
export const PAGE_H = 24 * 72;
export const MIN_PT = 14;

export const AREA = { x: 56, y: 158, w: 2480, h: 1314 };
export const BAND = { x: 48, y: 1486, w: 2496, h: 198 };
export const TITLE = { x: 1636, y: 1486, w: 900, h: 198 };
export const LEGEND = { x: 56, y: 1486, w: 1564, h: 198 };

export const INK = '#111827';
export const MUTED = '#1F2937';
export const NAVY = '#0B1B33';
export const PAPER = '#F7F4EA';
export const WHITE = '#FFFFFF';
export const GOLD = '#FAC400';
export const GOLD_INK = '#7A5C00';
export const CREAM = '#F3E8C2';

export const TONES = {
  det: { zone: '#E7F8FB', stroke: '#0E7490', bar: '#0891B2', title: '#155E75' },
  ai: { zone: '#F3EEFF', stroke: '#6D28D9', bar: '#7C3AED', title: '#5B21B6' },
  gw: { zone: '#FFF6D8', stroke: '#A16207', bar: '#C4920A', title: '#7A5C00' },
  gov: { zone: '#F1F5F9', stroke: '#475569', bar: '#64748B', title: '#1E293B' },
  human: { zone: '#E7F7F4', stroke: '#0F766E', bar: '#0D9488', title: '#115E59' },
  review: { zone: '#FFF1E6', stroke: '#C2410C', bar: '#EA580C', title: '#9A3412' },
  deploy: { zone: '#E8F0FE', stroke: '#1D4ED8', bar: '#2563EB', title: '#1E3A8A' },
  ok: { zone: '#E7F8EF', stroke: '#047857', bar: '#059669', title: '#065F46' },
  later: { zone: '#F8FAFC', stroke: '#64748B', bar: '#94A3B8', title: '#334155', dash: true },
  lane: { zone: '#F8FAFC', stroke: '#334155', bar: '#0B1B33', title: '#0B1B33' },
  neutral: { zone: '#FFFFFF', stroke: '#334155', bar: '#0B1B33', title: '#0B1B33' },
};

export function loadFonts(dir, parse) {
  const load = (file) => parse(fs.readFileSync(`${dir}/${file}`));
  return {
    regular: load('IBMPlexSans-Regular.ttf'),
    semibold: load('IBMPlexSans-SemiBold.ttf'),
    bold: load('IBMPlexSans-Bold.ttf'),
  };
}

function esc(s) {
  return String(s).replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;');
}

// opentype.js toPathData() emits NaN on some IBM Plex runs. Commands themselves are finite.
function commandsToPath(commands) {
  const n = (v) => (Number.isFinite(v) ? v.toFixed(2) : '0');
  let d = '';
  for (const c of commands) {
    if (c.type === 'M') d += `M${n(c.x)} ${n(c.y)}`;
    else if (c.type === 'L') d += `L${n(c.x)} ${n(c.y)}`;
    else if (c.type === 'Q') d += `Q${n(c.x1)} ${n(c.y1)} ${n(c.x)} ${n(c.y)}`;
    else if (c.type === 'C') d += `C${n(c.x1)} ${n(c.y1)} ${n(c.x2)} ${n(c.y2)} ${n(c.x)} ${n(c.y)}`;
    else if (c.type === 'Z') d += 'Z';
  }
  return d;
}

function intersects(a, b) {
  return a.x < b.x + b.w - 0.4 && a.x + a.w > b.x + 0.4 && a.y < b.y + b.h - 0.4 && a.y + a.h > b.y + 0.4;
}

function contains(outer, inner, pad = 0) {
  return (
    inner.x >= outer.x + pad - 0.4 &&
    inner.y >= outer.y + pad - 0.4 &&
    inner.x + inner.w <= outer.x + outer.w - pad + 0.4 &&
    inner.y + inner.h <= outer.y + outer.h - pad + 0.4
  );
}

function orient(ax, ay, bx, by, cx, cy) {
  const v = (by - ay) * (cx - bx) - (bx - ax) * (cy - by);
  if (Math.abs(v) < 0.05) return 0;
  return v > 0 ? 1 : -1;
}

function properCross(x1, y1, x2, y2, x3, y3, x4, y4) {
  const o1 = orient(x1, y1, x2, y2, x3, y3);
  const o2 = orient(x1, y1, x2, y2, x4, y4);
  const o3 = orient(x3, y3, x4, y4, x1, y1);
  const o4 = orient(x3, y3, x4, y4, x2, y2);
  if (o1 === 0 || o2 === 0 || o3 === 0 || o4 === 0) return false;
  return o1 !== o2 && o3 !== o4;
}

function pointInRect(x, y, r, inset = 0) {
  return x > r.x + inset && y > r.y + inset && x < r.x + r.w - inset && y < r.y + r.h - inset;
}

function segHitsRect(x1, y1, x2, y2, r) {
  const shrunk = { x: r.x + 1.6, y: r.y + 1.6, w: r.w - 3.2, h: r.h - 3.2 };
  if (shrunk.w <= 0 || shrunk.h <= 0) return false;
  if (pointInRect(x1, y1, shrunk, 0) || pointInRect(x2, y2, shrunk, 0)) return true;
  const edges = [
    [shrunk.x, shrunk.y, shrunk.x + shrunk.w, shrunk.y],
    [shrunk.x + shrunk.w, shrunk.y, shrunk.x + shrunk.w, shrunk.y + shrunk.h],
    [shrunk.x + shrunk.w, shrunk.y + shrunk.h, shrunk.x, shrunk.y + shrunk.h],
    [shrunk.x, shrunk.y + shrunk.h, shrunk.x, shrunk.y],
  ];
  return edges.some((e) => properCross(x1, y1, x2, y2, e[0], e[1], e[2], e[3]));
}

export class Sheet {
  constructor(fonts, meta) {
    this.fonts = fonts;
    this.meta = meta;
    this.area = AREA;
    this.nodes = new Map();
    this.zones = [];
    this.segments = [];
    this.texts = [];
    this.pills = [];
    this.under = [];
    this.shapes = [];
    this.chromeShapes = [];
    this.errors = [];
    this._chrome = false;
    this._figure = true;
  }

  fontOf(weight) {
    return this.fonts[weight] || this.fonts.regular;
  }

  em(font, size) {
    return ((font.ascender - font.descender) / font.unitsPerEm) * size;
  }

  asc(font, size) {
    return (font.ascender / font.unitsPerEm) * size;
  }

  width(weight, text, size) {
    return this.fontOf(weight).getAdvanceWidth(text, size);
  }

  assertGlyphs(weight, text, id) {
    const font = this.fontOf(weight);
    for (const ch of text) {
      if (font.charToGlyph(ch).name === '.notdef') {
        this.errors.push(`${id}: missing glyph ${JSON.stringify(ch)} U+${ch.codePointAt(0).toString(16)}`);
      }
    }
  }

  addText(rec) {
    if (rec.size < MIN_PT - 0.01) this.errors.push(`${rec.id}: ${rec.size} pt is under ${MIN_PT} pt`);
    this.assertGlyphs(rec.weight, rec.text, rec.id);
    const font = this.fontOf(rec.weight);
    const w = font.getAdvanceWidth(rec.text, rec.size);
    const box = {
      x: rec.x,
      y: rec.baseline - this.asc(font, rec.size),
      w,
      h: this.em(font, rec.size),
      id: rec.id,
    };
    this.texts.push({ ...rec, w, box, figure: this._figure });
    return box;
  }

  /** One line. x is the left of the glyphs unless align is set against `clip`. */
  text(id, text, x, baseline, { size = 14, weight = 'regular', fill = INK, align = 'left', clip = null } = {}) {
    const w = this.width(weight, text, size);
    let left = x;
    if (align === 'center') left = x - w / 2;
    if (align === 'right') left = x - w;
    const box = this.addText({ id, text, x: left, baseline, size, weight, fill });
    if (clip && !contains(clip, box, 1)) {
      this.errors.push(`${id}: text escapes its clip (${text})`);
    }
    return box;
  }

  shape(svg) {
    if (this._chrome) this.chromeShapes.push(svg);
    else this.shapes.push(svg);
  }

  roundRect(x, y, w, h, rx, attrs) {
    const parts = Object.entries(attrs)
      .filter(([, v]) => v !== undefined && v !== null)
      .map(([k, v]) => `${k}="${v}"`)
      .join(' ');
    this.shape(`<rect x="${x.toFixed(2)}" y="${y.toFixed(2)}" width="${w.toFixed(2)}" height="${h.toFixed(2)}" rx="${rx}" ${parts}/>`);
  }

  zone(id, x, y, w, h, title, toneName) {
    const tone = TONES[toneName];
    this.roundRect(x, y, w, h, 10, { fill: tone.zone, stroke: tone.stroke, 'stroke-width': 1.6 });
    this.shape(
      `<rect x="${x.toFixed(2)}" y="${y.toFixed(2)}" width="7" height="${h.toFixed(2)}" rx="3" fill="${tone.bar}"/>`,
    );
    const clip = { x, y, w, h: 34 };
    const baseline = y + 24;
    this.text(`${id}-title`, title, x + 18, baseline, {
      size: 16,
      weight: 'semibold',
      fill: tone.title,
      clip: { x, y, w: w - 8, h: 36 },
    });
    // 15 pt is the minimum that still clears 14. Guard above uses MIN_PT.
    const z = { id, x, y, w, h, titleBand: { x, y, w, h: 36 } };
    this.zones.push(z);
    return z;
  }

  node(id, x, y, w, h, lines, toneName, opts = {}) {
    const tone = TONES[toneName];
    const dash = opts.dash || tone.dash ? '7 4' : undefined;
    this.roundRect(x, y, w, h, 8, {
      fill: WHITE,
      stroke: tone.stroke,
      'stroke-width': opts.badge ? 2.4 : 1.5,
      'stroke-dasharray': dash,
    });
    this.shape(
      `<rect x="${(x + 1).toFixed(2)}" y="${(y + 8).toFixed(2)}" width="7" height="${(h - 16).toFixed(2)}" rx="2" fill="${tone.bar}"/>`,
    );
    const specs = (Array.isArray(lines) ? lines : [lines]).map((line, i) => {
      if (typeof line === 'string') {
        return {
          text: line,
          size: i === 0 ? 16 : 14,
          weight: i === 0 ? 'semibold' : 'regular',
          fill: i === 0 ? INK : MUTED,
        };
      }
      return line;
    });
    let textBox = { x, y, w, h };
    if (opts.badge) {
      const reserved = this.badge(id, { x, y, w, h }, opts.badge, tone);
      textBox = { x, y: y + reserved, w, h: h - reserved };
    }
    this.layoutInBox(id, specs, textBox, opts.align || 'center');
    const rec = { id, x, y, w, h };
    this.nodes.set(id, rec);
    return rec;
  }

  // Badge sits on its own row so it cannot collide with the title. Returns the reserved height.
  badge(id, box, label, tone) {
    const size = 14;
    const weight = 'bold';
    const tw = this.width(weight, label, size);
    const font = this.fontOf(weight);
    const th = this.em(font, size);
    const padX = 7;
    const padY = 3;
    const pw = tw + padX * 2;
    const ph = th + padY * 2;
    const x = box.x + (box.w - pw) / 2;
    const y = box.y + 6;
    this.roundRect(x, y, pw, ph, 4, { fill: tone.zone, stroke: tone.stroke, 'stroke-width': 1.2 });
    const baseline = y + padY + this.asc(font, size);
    this.addText({
      id: `${id}-badge`,
      text: label,
      x: x + padX,
      baseline,
      size,
      weight,
      fill: tone.title,
    });
    return 6 + ph + 6;
  }

  // Horizontal swimlane. Title is a left label, not a top band, so cards can use the full row.
  band(id, x, y, w, h, title, toneName) {
    const tone = TONES[toneName];
    this.roundRect(x, y, w, h, 8, { fill: tone.zone, stroke: tone.stroke, 'stroke-width': 1.15 });
    this.shape(
      `<rect x="${x.toFixed(2)}" y="${(y + 6).toFixed(2)}" width="7" height="${(h - 12).toFixed(2)}" rx="2" fill="${tone.bar}"/>`,
    );
    const size = 14;
    const weight = 'semibold';
    const font = this.fontOf(weight);
    const baseline = y + h / 2 + (this.asc(font, size) - this.em(font, size) / 2);
    this.text(`${id}-label`, title, x + 18, baseline, { size, weight, fill: tone.title });
    return { id, x, y, w, h };
  }

  layoutInBox(id, specs, box, align) {
    const gap = 3;
    const heights = specs.map((s) => this.em(this.fontOf(s.weight || 'regular'), s.size || 14));
    const total = heights.reduce((a, b) => a + b, 0) + gap * Math.max(0, specs.length - 1);
    if (total + 12 > box.h) {
      this.errors.push(`${id}: needs ${Math.ceil(total + 12)} pt height, has ${box.h.toFixed(1)}`);
    }
    let top = box.y + (box.h - total) / 2;
    const innerL = box.x + 18;
    const innerR = box.x + box.w - 12;
    const avail = innerR - innerL;
    specs.forEach((s, i) => {
      const size = s.size || 14;
      const weight = s.weight || 'regular';
      const w = this.width(weight, s.text, size);
      if (w > avail + 0.6) {
        this.errors.push(`${id}: "${s.text}" is ${w.toFixed(1)} pt wide, box allows ${avail.toFixed(1)}`);
      }
      let x = innerL;
      if (align === 'center') x = innerL + (avail - w) / 2;
      if (align === 'right') x = innerR - w;
      const font = this.fontOf(weight);
      const baseline = top + this.asc(font, size);
      this.addText({
        id: `${id}-l${i}`,
        text: s.text,
        x,
        baseline,
        size,
        weight,
        fill: s.fill || INK,
      });
      top += heights[i] + gap;
    });
  }

  pill(id, cx, cy, text, { size = 14, weight = 'semibold', fill = INK } = {}) {
    const w = this.width(weight, text, size);
    const font = this.fontOf(weight);
    const th = this.em(font, size);
    const padX = 8;
    const padY = 5;
    const pw = w + padX * 2;
    const ph = th + padY * 2;
    const x = cx - pw / 2;
    const y = cy - ph / 2;
    this.roundRect(x, y, pw, ph, 5, { fill: WHITE, stroke: NAVY, 'stroke-width': 1.1 });
    const baseline = y + padY + this.asc(font, size);
    this.addText({ id, text, x: x + padX, baseline, size, weight, fill });
    const rec = { id, x, y, w: pw, h: ph };
    this.pills.push(rec);
    return rec;
  }

  banner(id, x, y, w, h, text) {
    this.roundRect(x, y, w, h, 8, { fill: '#FFF8E6', stroke: '#E6D39A', 'stroke-width': 1 });
    this.shape(`<rect x="${x.toFixed(2)}" y="${y.toFixed(2)}" width="7" height="${h.toFixed(2)}" rx="3" fill="${GOLD}"/>`);
    const size = 16;
    const weight = 'semibold';
    const tw = this.width(weight, text, size);
    if (tw > w - 36) this.errors.push(`${id}: banner text is ${tw.toFixed(1)} pt in ${w - 36} pt`);
    const font = this.fontOf(weight);
    const baseline = y + (h - this.em(font, size)) / 2 + this.asc(font, size);
    this.addText({
      id,
      text,
      x: x + 20,
      baseline,
      size,
      weight,
      fill: NAVY,
    });
  }

  note(id, x, y, w, h, text, { size = 14, weight = 'regular', fill = INK } = {}) {
    this.roundRect(x, y, w, h, 8, { fill: WHITE, stroke: '#D6D3C9', 'stroke-width': 1 });
    const font = this.fontOf(weight);
    const words = text.split(/\s+/);
    const lines = [];
    let cur = '';
    const max = w - 28;
    for (const word of words) {
      const trial = cur ? `${cur} ${word}` : word;
      if (font.getAdvanceWidth(trial, size) <= max) cur = trial;
      else {
        if (cur) lines.push(cur);
        cur = word;
      }
    }
    if (cur) lines.push(cur);
    const specs = lines.map((t) => ({ text: t, size, weight, fill }));
    this.layoutInBox(id, specs, { x, y, w, h }, 'left');
    this.nodes.set(id, { id, x, y, w, h, note: true });
  }

  anchor(id, side) {
    const n = this.nodes.get(id);
    if (!n) throw new Error(`unknown node ${id}`);
    const cx = n.x + n.w / 2;
    const cy = n.y + n.h / 2;
    if (side === 'left') return { x: n.x, y: cy };
    if (side === 'right') return { x: n.x + n.w, y: cy };
    if (side === 'top') return { x: cx, y: n.y };
    if (side === 'bottom') return { x: cx, y: n.y + n.h };
    throw new Error(`bad side ${side}`);
  }

  arrow(points, { id = 'arrow', head = true, dashed = false, color = NAVY } = {}) {
    if (points.length < 2) this.errors.push(`${id}: arrow needs 2 points`);
    const tip = points[points.length - 1];
    const prev = points[points.length - 2];
    let draw = points.map((p) => ({ x: p.x, y: p.y }));
    if (head) {
      const dx = tip.x - prev.x;
      const dy = tip.y - prev.y;
      const len = Math.hypot(dx, dy);
      const headLen = 12;
      if (len < headLen + 4) this.errors.push(`${id}: last segment ${len.toFixed(1)} pt is shorter than the arrow head`);
      else {
        const ux = dx / len;
        const uy = dy / len;
        const base = { x: tip.x - ux * headLen, y: tip.y - uy * headLen };
        draw[draw.length - 1] = base;
        const px = -uy;
        const py = ux;
        const left = { x: base.x + px * 5.5, y: base.y + py * 5.5 };
        const right = { x: base.x - px * 5.5, y: base.y - py * 5.5 };
        this.shape(
          `<polygon points="${tip.x.toFixed(2)},${tip.y.toFixed(2)} ${left.x.toFixed(2)},${left.y.toFixed(2)} ${right.x.toFixed(2)},${right.y.toFixed(2)}" fill="${color}"/>`,
        );
      }
    }
    const d = draw.map((p, i) => `${i === 0 ? 'M' : 'L'}${p.x.toFixed(2)} ${p.y.toFixed(2)}`).join(' ');
    const dash = dashed ? ' stroke-dasharray="7 5"' : '';
    this.shape(
      `<path d="${d}" fill="none" stroke="${color}" stroke-width="1.8" stroke-linejoin="round" stroke-linecap="round"${dash}/>`,
    );
    for (let i = 0; i < draw.length - 1; i += 1) {
      this.segments.push({
        id: `${id}#${i}`,
        x1: draw[i].x,
        y1: draw[i].y,
        x2: draw[i + 1].x,
        y2: draw[i + 1].y,
      });
    }
  }

  link(fromId, toId, fromSide, toSide, opts = {}) {
    const a = this.anchor(fromId, fromSide);
    const b = this.anchor(toId, toSide);
    let pts;
    if (opts.via) pts = [a, ...opts.via, b];
    else if ((fromSide === 'right' || fromSide === 'left') && Math.abs(a.y - b.y) < 0.8 && (toSide === 'left' || toSide === 'right')) {
      pts = [a, b];
    } else if ((fromSide === 'bottom' || fromSide === 'top') && Math.abs(a.x - b.x) < 0.8 && (toSide === 'top' || toSide === 'bottom')) {
      pts = [a, b];
    } else if (fromSide === 'right' || fromSide === 'left') {
      const mid = (a.x + b.x) / 2;
      pts = [a, { x: mid, y: a.y }, { x: mid, y: b.y }, b];
    } else {
      const mid = (a.y + b.y) / 2;
      pts = [a, { x: a.x, y: mid }, { x: b.x, y: mid }, b];
    }
    this.arrow(pts, {
      id: opts.id || `${fromId}-${toId}`,
      head: opts.head !== false,
      dashed: opts.dashed,
      color: opts.color,
    });
  }

  box(id) {
    const n = this.nodes.get(id);
    if (!n) throw new Error(`unknown node ${id}`);
    return n;
  }

  cx(id) {
    const n = this.box(id);
    return n.x + n.w / 2;
  }

  cy(id) {
    const n = this.box(id);
    return n.y + n.h / 2;
  }

  drawChrome(legendItems, logoInner) {
    this._chrome = true;
    this._figure = false;
    const m = this.meta;
    const under = (svg) => this.under.push(svg);
    under(`<rect x="0" y="0" width="${PAGE_W}" height="${PAGE_H}" fill="${PAPER}"/>`);
    under(`<rect x="28" y="28" width="${PAGE_W - 56}" height="${PAGE_H - 56}" fill="none" stroke="${NAVY}" stroke-width="2.2"/>`);
    under(`<rect x="36" y="36" width="${PAGE_W - 72}" height="${PAGE_H - 72}" fill="none" stroke="${NAVY}" stroke-width="0.7"/>`);

    const hx = 44;
    const hy = 44;
    const hw = 2492;
    const hh = 86;
    under(`<rect x="${hx}" y="${hy}" width="${hw}" height="${hh}" fill="${NAVY}"/>`);
    under(`<rect x="${hx}" y="${hy + hh}" width="${hw}" height="5" fill="${GOLD}"/>`);

    const headerClip = { x: hx, y: hy, w: hw, h: hh };
    this.text('hdr-name', 'MERIDIAN PRECISION SYSTEMS', hx + 22, hy + 36, {
      size: 22,
      weight: 'bold',
      fill: WHITE,
      clip: headerClip,
    });
    this.text('hdr-sub', 'Quality and Engineering Issue Resolution', hx + 22, hy + 68, {
      size: 14,
      weight: 'regular',
      fill: CREAM,
      clip: headerClip,
    });
    this.text('hdr-title', m.kicker, hx + hw - 22, hy + 36, {
      size: 22,
      weight: 'bold',
      fill: WHITE,
      align: 'right',
      clip: headerClip,
    });
    this.text('hdr-by', 'Prepared by StatusNeo', hx + hw - 22, hy + 68, {
      size: 16,
      weight: 'semibold',
      fill: GOLD,
      align: 'right',
      clip: headerClip,
    });

    // Legend panel
    this.roundRect(LEGEND.x, LEGEND.y, LEGEND.w, LEGEND.h, 8, {
      fill: WHITE,
      stroke: NAVY,
      'stroke-width': 1.3,
    });
    this.text('legend-h', 'LEGEND', LEGEND.x + 16, LEGEND.y + 28, {
      size: 14,
      weight: 'bold',
      fill: NAVY,
    });
    const colW = (LEGEND.w - 32) / 2;
    legendItems.forEach((item, i) => {
      const col = i < Math.ceil(legendItems.length / 2) ? 0 : 1;
      const row = col === 0 ? i : i - Math.ceil(legendItems.length / 2);
      const x = LEGEND.x + 16 + col * colW;
      const y = LEGEND.y + 44 + row * 36;
      const tone = TONES[item.tone];
      this.roundRect(x, y, 22, 22, 3, { fill: tone.zone, stroke: tone.stroke, 'stroke-width': 1.4 });
      this.shape(`<rect x="${x.toFixed(2)}" y="${y.toFixed(2)}" width="5" height="22" fill="${tone.bar}"/>`);
      this.text(`legend-${i}`, item.label, x + 32, y + 16, { size: 14, weight: 'regular', fill: INK });
    });

    // Title block
    this.roundRect(TITLE.x, TITLE.y, TITLE.w, TITLE.h, 8, { fill: WHITE, stroke: NAVY, 'stroke-width': 1.6 });
    const logoSize = 64;
    const logoX = TITLE.x + 14;
    const logoY = TITLE.y + 14;
    const scale = logoSize / 64;
    this.shape(`<g transform="translate(${logoX} ${logoY}) scale(${scale})">${logoInner}</g>`);

    const tx = logoX + logoSize + 14;
    this.text('tb-name', 'MERIDIAN PRECISION SYSTEMS', tx, TITLE.y + 34, {
      size: 15,
      weight: 'bold',
      fill: NAVY,
    });
    this.text('tb-sub', 'Quality and Engineering Issue Resolution', tx, TITLE.y + 56, {
      size: 14,
      weight: 'regular',
      fill: MUTED,
    });
    this.shape(
      `<line x1="${TITLE.x + 12}" y1="${TITLE.y + 84}" x2="${TITLE.x + TITLE.w - 12}" y2="${TITLE.y + 84}" stroke="${NAVY}" stroke-width="0.8"/>`,
    );
    this.text('tb-title', m.title, TITLE.x + 16, TITLE.y + 108, {
      size: 16,
      weight: 'bold',
      fill: NAVY,
    });
    this.text('tb-meta', `${m.sheetId}  ·  Issue ${m.issue}      Version v1.0      Date 2026-10-14`, TITLE.x + 16, TITLE.y + 132, {
      size: 14,
      weight: 'regular',
      fill: INK,
    });
    this.text('tb-by', 'Prepared by StatusNeo', TITLE.x + 16, TITLE.y + 154, {
      size: 14,
      weight: 'semibold',
      fill: GOLD_INK,
    });
    this.text('tb-copy', '(c) 2026 Bryan C. Weis and Common Consulting Company', TITLE.x + 16, TITLE.y + 178, {
      size: 14,
      weight: 'regular',
      fill: INK,
    });
    this._chrome = false;
  }

  validate() {
    const frame = { x: 40, y: 40, w: PAGE_W - 80, h: PAGE_H - 80 };
    for (const t of this.texts) {
      if (!contains(frame, t.box, 0)) this.errors.push(`${t.id}: text is outside the sheet frame`);
    }

    const nodeList = [...this.nodes.values()];
    for (let i = 0; i < nodeList.length; i += 1) {
      for (let j = i + 1; j < nodeList.length; j += 1) {
        if (intersects(nodeList[i], nodeList[j])) {
          this.errors.push(`nodes overlap: ${nodeList[i].id} and ${nodeList[j].id}`);
        }
      }
      for (const z of this.zones) {
        if (intersects(nodeList[i], z) && !contains(z, nodeList[i], 8)) {
          this.errors.push(`node ${nodeList[i].id} crosses zone ${z.id}`);
        }
        if (contains(z, nodeList[i], 0) && nodeList[i].y < z.y + 38) {
          this.errors.push(`node ${nodeList[i].id} hits the title band of ${z.id}`);
        }
      }
      if (nodeList[i].y + nodeList[i].h > AREA.y + AREA.h + 0.5 || nodeList[i].y < AREA.y - 0.5) {
        this.errors.push(`node ${nodeList[i].id} is outside the diagram area vertically`);
      }
    }
    for (let i = 0; i < this.zones.length; i += 1) {
      for (let j = i + 1; j < this.zones.length; j += 1) {
        if (intersects(this.zones[i], this.zones[j])) {
          this.errors.push(`zones overlap: ${this.zones[i].id} and ${this.zones[j].id}`);
        }
      }
    }

    const textBoxes = this.texts.map((t) => ({
      x: t.box.x - 1.5,
      y: t.box.y - 1.5,
      w: t.box.w + 3,
      h: t.box.h + 3,
      id: t.id,
    }));
    for (let i = 0; i < textBoxes.length; i += 1) {
      for (let j = i + 1; j < textBoxes.length; j += 1) {
        if (intersects(textBoxes[i], textBoxes[j])) {
          this.errors.push(`text overlap: ${textBoxes[i].id} and ${textBoxes[j].id}`);
        }
      }
    }

    for (const seg of this.segments) {
      for (const n of nodeList) {
        if (segHitsRect(seg.x1, seg.y1, seg.x2, seg.y2, n)) {
          this.errors.push(`line ${seg.id} crosses node ${n.id}`);
        }
      }
      for (const t of textBoxes) {
        if (segHitsRect(seg.x1, seg.y1, seg.x2, seg.y2, t)) {
          this.errors.push(`line ${seg.id} crosses text ${t.id}`);
        }
      }
      for (const p of this.pills) {
        if (segHitsRect(seg.x1, seg.y1, seg.x2, seg.y2, p)) {
          this.errors.push(`line ${seg.id} crosses pill ${p.id}`);
        }
      }
    }
    for (let i = 0; i < this.segments.length; i += 1) {
      for (let j = i + 1; j < this.segments.length; j += 1) {
        const a = this.segments[i];
        const b = this.segments[j];
        if (properCross(a.x1, a.y1, a.x2, a.y2, b.x1, b.y1, b.x2, b.y2)) {
          this.errors.push(`lines cross: ${a.id} and ${b.id}`);
        }
      }
    }
    for (const p of this.pills) {
      for (const n of nodeList) {
        if (intersects(p, n)) this.errors.push(`pill ${p.id} overlaps node ${n.id}`);
      }
    }

    // Header lines must not collide with each other — already in text overlap.
    // Title block copyright must be the lowest text in the title block.
    const copy = this.texts.find((t) => t.id === 'tb-copy');
    if (!copy) this.errors.push('title block is missing the copyright line');
    else if (copy.box.y + copy.box.h > TITLE.y + TITLE.h - 4) {
      this.errors.push('copyright line is clipped by the title block');
    }

    if (this.errors.length) {
      const unique = [...new Set(this.errors)];
      const err = new Error(unique.slice(0, 50).join('\n'));
      err.errors = unique;
      throw err;
    }
  }

  textPaths(figureOnly) {
    return this.texts
      .filter((t) => !figureOnly || t.figure)
      .map((t) => {
        const font = this.fontOf(t.weight);
        const path = font.getPath(t.text, t.x, t.baseline, t.size);
        const d = commandsToPath(path.commands);
        if (!d) return '';
        if (d.includes('NaN') || d.includes('Infinity')) {
          this.errors.push(`${t.id}: outline is not finite`);
        }
        return `<path d="${d}" fill="${t.fill}"/>`;
      })
      .join('\n');
  }

  renderSheet() {
    const body = `${this.under.join('\n')}\n${this.shapes.join('\n')}\n${this.chromeShapes.join('\n')}\n${this.textPaths(false)}`;
    return svgDoc({
      width: '36in',
      height: '24in',
      viewBox: `0 0 ${PAGE_W} ${PAGE_H}`,
      title: `${this.meta.kicker} — Meridian Precision Systems`,
      desc: '(c) 2026 Bryan C. Weis and Common Consulting Company',
      body,
    });
  }

  renderFigure() {
    const box = AREA;
    const bg = `<rect x="${box.x}" y="${box.y}" width="${box.w}" height="${box.h}" fill="${PAPER}"/>`;
    const body = `${bg}\n${this.shapes.join('\n')}\n${this.textPaths(true)}`;
    const widthIn = (box.w / 72).toFixed(3);
    const heightIn = (box.h / 72).toFixed(3);
    return svgDoc({
      width: `${widthIn}in`,
      height: `${heightIn}in`,
      viewBox: `${box.x} ${box.y} ${box.w} ${box.h}`,
      title: `${this.meta.title} — Meridian Precision Systems`,
      desc: '(c) 2026 Bryan C. Weis and Common Consulting Company',
      body,
    });
  }
}

export function svgDoc({ width, height, viewBox, title, desc, body }) {
  return `<?xml version="1.0" encoding="UTF-8"?>
<svg xmlns="http://www.w3.org/2000/svg" width="${width}" height="${height}" viewBox="${viewBox}" role="img" aria-labelledby="t d">
<title id="t">${esc(title)}</title>
<desc id="d">${esc(desc)}</desc>
<!-- Copyright (c) 2026 Bryan C. Weis and Common Consulting Company -->
${body}
</svg>
`;
}

export function logoInner(svgText) {
  return svgText.replace(/^[\s\S]*?<svg[^>]*>/, '').replace(/<\/svg>\s*$/, '');
}
