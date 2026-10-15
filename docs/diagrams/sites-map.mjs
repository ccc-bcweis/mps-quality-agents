// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company
// Stylized site map for the opening slide. Invented coast. Not a real place.

import { collectLabelIssues, glyphPath, svgDoc, INK, MUTED, NAVY, PAPER, WHITE } from './sheet.mjs';
import { PLANTS } from './plants.mjs';

const W = 960;
const H = 540;

function boxOf(font, text, size, left, baseline) {
  const w = font.getAdvanceWidth(text, size);
  const asc = (font.ascender / font.unitsPerEm) * size;
  const em = ((font.ascender - font.descender) / font.unitsPerEm) * size;
  return { x: left, y: baseline - asc, w, h: em };
}

function addLabel(font, labels, paths, id, text, left, baseline, size, weight, fill, clip) {
  const face = weight === 'bold' ? font.bold : weight === 'semibold' ? font.semibold : font.regular;
  const box = boxOf(face, text, size, left, baseline);
  labels.push({ id, text, box, clip });
  const d = glyphPath(face, text, left, baseline, size);
  if (d) paths.push(`<path d="${d}" fill="${fill}"/>`);
  return box;
}

function addCentered(font, labels, paths, id, text, cx, baseline, size, weight, fill, clip) {
  const face = weight === 'bold' ? font.bold : weight === 'semibold' ? font.semibold : font.regular;
  const w = face.getAdvanceWidth(text, size);
  return addLabel(font, labels, paths, id, text, cx - w / 2, baseline, size, weight, fill, clip);
}

export function drawSiteMap(fonts) {
  const labels = [];
  const paths = [];
  const obstacles = [];
  const page = { x: 8, y: 8, w: W - 16, h: H - 16 };
  const shapes = [];

  shapes.push(`<rect width="${W}" height="${H}" fill="${PAPER}"/>`);
  shapes.push(`<rect x="18" y="18" width="${W - 36}" height="${H - 36}" fill="none" stroke="${NAVY}" stroke-width="1.6"/>`);

  addLabel(fonts, labels, paths, 'title', 'Meridian Precision Systems', 36, 48, 20, 'bold', NAVY, page);
  addLabel(fonts, labels, paths, 'sub', 'Three fictional plants on an invented coast', 36, 74, 14, 'regular', MUTED, page);
  addCentered(fonts, labels, paths, 'badge', 'Illustrative', 860, 48, 16, 'semibold', '#7A5C00', page);

  // Diagrammatic land. A rounded block with a notch. Not a traced coastline.
  shapes.push(
    `<path d="M230 118 H560 Q620 118 620 178 V360 Q620 420 560 420 H230 Q170 420 170 360 V300 Q250 286 170 272 V178 Q170 118 230 118 Z" fill="#E4EFE6" stroke="${NAVY}" stroke-width="1.8"/>`,
  );
  addLabel(fonts, labels, paths, 'water', 'Open water', 36, 150, 14, 'regular', MUTED, { x: 28, y: 120, w: 130, h: 40 });
  addLabel(fonts, labels, paths, 'north', 'North', 36, 186, 14, 'semibold', NAVY, { x: 28, y: 160, w: 130, h: 40 });
  shapes.push(`<polygon points="58,158 52,174 64,174" fill="${NAVY}"/>`);

  const pins = [
    { plant: PLANTS[0], x: 300, y: 168 },
    { plant: PLANTS[1], x: 490, y: 270 },
    { plant: PLANTS[2], x: 360, y: 392 },
  ];
  const cardX = 668;
  const cardW = 260;
  const cardH = 120;
  const cardY = [100, 230, 360];

  pins.forEach((pin, i) => {
    const card = { x: cardX, y: cardY[i], w: cardW, h: cardH };
    shapes.push(
      `<rect x="${card.x}" y="${card.y}" width="${card.w}" height="${card.h}" rx="8" fill="${WHITE}" stroke="${NAVY}" stroke-width="1.4"/>`,
    );
    shapes.push(`<rect x="${card.x}" y="${card.y + 8}" width="6" height="${card.h - 16}" rx="2" fill="#FAC400"/>`);
    const plant = pin.plant;
    addLabel(fonts, labels, paths, `c${plant.id}-a`, `Plant ${plant.id}`, card.x + 20, card.y + 26, 16, 'semibold', NAVY, card);
    addLabel(fonts, labels, paths, `c${plant.id}-b`, plant.name, card.x + 20, card.y + 54, 16, 'semibold', INK, card);
    addLabel(fonts, labels, paths, `c${plant.id}-c`, plant.role, card.x + 20, card.y + 80, 14, 'regular', INK, card);
    addLabel(fonts, labels, paths, `c${plant.id}-d`, plant.work, card.x + 20, card.y + 104, 14, 'regular', MUTED, card);

    shapes.push(`<circle cx="${pin.x}" cy="${pin.y}" r="16" fill="#FAC400" stroke="${NAVY}" stroke-width="1.6"/>`);
    addCentered(fonts, labels, paths, `pin-${plant.id}`, plant.id, pin.x, pin.y + 5, 14, 'bold', NAVY, {
      x: pin.x - 14,
      y: pin.y - 14,
      w: 28,
      h: 28,
    });

    const y = pin.y;
    const x1 = pin.x + 22;
    shapes.push(`<line x1="${x1}" y1="${y}" x2="${card.x}" y2="${y}" stroke="${NAVY}" stroke-width="1.4"/>`);
    obstacles.push({ id: `leader-${plant.id}`, x: x1, y: y - 2, w: card.x - x1, h: 4 });
  });

  addLabel(
    fonts,
    labels,
    paths,
    'foot',
    'Fictional coast. Not a real city. Not to scale.',
    36,
    492,
    14,
    'regular',
    INK,
    page,
  );
  addLabel(
    fonts,
    labels,
    paths,
    'copy',
    '(c) 2026 Bryan C. Weis and Common Consulting Company',
    36,
    518,
    14,
    'regular',
    MUTED,
    page,
  );

  const issues = collectLabelIssues(labels, obstacles);
  if (issues.length) {
    const err = new Error(issues.slice(0, 40).join('\n'));
    err.errors = issues;
    throw err;
  }

  return svgDoc({
    width: '13.333in',
    height: '7.5in',
    viewBox: `0 0 ${W} ${H}`,
    title: 'Meridian Precision Systems plants — illustrative',
    desc: 'Illustrative fictional coast. (c) 2026 Bryan C. Weis and Common Consulting Company',
    body: `${shapes.join('\n')}\n${paths.join('\n')}`,
  });
}
