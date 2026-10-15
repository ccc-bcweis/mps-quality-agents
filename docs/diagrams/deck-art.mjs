// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company
// Empty picture frames for the opening slide. The prompts live in the deck outline.

import { collectLabelIssues, glyphPath, svgDoc, INK, MUTED, NAVY, PAPER } from './sheet.mjs';

const W = 960;
const H = 540;

function boxOf(font, text, size, left, baseline) {
  const w = font.getAdvanceWidth(text, size);
  const asc = (font.ascender / font.unitsPerEm) * size;
  const em = ((font.ascender - font.descender) / font.unitsPerEm) * size;
  return { x: left, y: baseline - asc, w, h: em };
}

function frame(fonts, { file, caption }) {
  const labels = [];
  const paths = [];
  const page = { x: 24, y: 24, w: W - 48, h: H - 48 };
  const face = (weight) => (weight === 'bold' ? fonts.bold : weight === 'semibold' ? fonts.semibold : fonts.regular);
  const add = (id, text, cx, baseline, size, weight, fill) => {
    const font = face(weight);
    const w = font.getAdvanceWidth(text, size);
    const left = cx - w / 2;
    const box = boxOf(font, text, size, left, baseline);
    labels.push({ id, text, box, clip: page });
    const d = glyphPath(font, text, left, baseline, size);
    if (d) paths.push(`<path d="${d}" fill="${fill}"/>`);
  };

  add('mark', 'Illustrative', W / 2, 210, 28, 'bold', NAVY);
  add('here', 'Picture goes here', W / 2, 256, 16, 'semibold', INK);
  add('cap', caption, W / 2, 290, 14, 'regular', MUTED);
  add('prompt', 'Generation prompt is on the K7 slide in the deck outline.', W / 2, 322, 14, 'regular', INK);
  add('copy', '(c) 2026 Bryan C. Weis and Common Consulting Company', W / 2, 370, 14, 'regular', MUTED);

  const gold = { id: 'gold-rule', x: 80, y: 150, w: W - 160, h: 6 };
  const issues = collectLabelIssues(labels, [gold]);
  if (issues.length) throw new Error(issues.join('\n'));

  const body = [
    `<rect width="${W}" height="${H}" fill="${PAPER}"/>`,
    `<rect x="28" y="28" width="${W - 56}" height="${H - 56}" fill="none" stroke="${NAVY}" stroke-width="2"/>`,
    `<rect x="40" y="40" width="${W - 80}" height="${H - 80}" fill="none" stroke="${NAVY}" stroke-width="0.8" stroke-dasharray="10 8"/>`,
    `<rect x="${gold.x}" y="${gold.y}" width="${gold.w}" height="${gold.h}" fill="#FAC400"/>`,
    paths.join('\n'),
  ].join('\n');

  return [
    file,
    svgDoc({
      width: '13.333in',
      height: '7.5in',
      viewBox: `0 0 ${W} ${H}`,
      title: `${caption} — Illustrative`,
      desc: 'Illustrative placeholder. (c) 2026 Bryan C. Weis and Common Consulting Company',
      body,
    }),
  ];
}

export function drawDeckPlaceholders(fonts) {
  return [
    frame(fonts, {
      file: 'exterior-illustrative.svg',
      caption: 'Plant exterior. Fictional site. No real building.',
    }),
    frame(fonts, {
      file: 'cleanroom-illustrative.svg',
      caption: 'Cleanroom aisle. Fictional tools. No real building.',
    }),
  ];
}
