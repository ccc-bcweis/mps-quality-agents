// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company
// Regenerates vector sheets, ARCH D PDFs, and 150 dpi previews.
// Text is converted to outlines (IBM Plex Sans, SIL OFL). No live font in the PDF.

import { spawn } from 'node:child_process';
import fs from 'node:fs';
import path from 'node:path';
import { pathToFileURL, fileURLToPath } from 'node:url';
import { collectLabelIssues, loadFonts, Sheet, logoInner } from './sheet.mjs';
import { SHEETS } from './sheets.mjs';
import { drawSiteMap } from './sites-map.mjs';
import { drawDeckPlaceholders } from './deck-art.mjs';

const here = path.dirname(fileURLToPath(import.meta.url));
const repo = path.resolve(here, '..', '..');
const tools = process.env.MPS_DIAGRAM_TOOLS || path.join(process.env.TEMP || 'C:\\Users\\bcw\\AppData\\Local\\Temp', 'mps-diagram-tools');
const printDir = path.join(here, 'print');
const mermaidDir = path.join(printDir, 'mermaid');
const previewDir = path.join(printDir, 'preview');

const args = process.argv.slice(2);
const onlyIdx = args.indexOf('--only');
const only = onlyIdx >= 0 ? args[onlyIdx + 1] : null;
const skipMermaid = args.includes('--skip-mermaid');
const checkOnly = args.includes('--check');

function edgePath() {
  const candidates = [
    'C:\\Program Files (x86)\\Microsoft\\Edge\\Application\\msedge.exe',
    'C:\\Program Files\\Microsoft\\Edge\\Application\\msedge.exe',
  ];
  const found = candidates.find((p) => fs.existsSync(p));
  if (!found) throw new Error('Microsoft Edge (msedge.exe) was not found');
  return found;
}

async function loadParse() {
  const mjs = path.join(tools, 'node_modules', 'opentype.js', 'dist', 'opentype.mjs');
  const mod = await import(pathToFileURL(mjs).href);
  if (typeof mod.parse !== 'function') throw new Error('opentype.parse export is missing');
  return mod.parse;
}

async function loadPuppeteer() {
  const mjs = path.join(tools, 'node_modules', 'puppeteer-core', 'lib', 'puppeteer', 'puppeteer-core.js');
  const mod = await import(pathToFileURL(mjs).href);
  return mod.default;
}

function build(fonts, meta, logo) {
  const sheet = new Sheet(fonts, meta);
  meta.draw(sheet);
  sheet.drawChrome(meta.legend, logo);
  return sheet;
}

function writeSheets(sheet, meta) {
  fs.mkdirSync(printDir, { recursive: true });
  const arch = path.join(printDir, `${meta.slug}-archD.svg`);
  const fig = path.join(printDir, `${meta.slug}.svg`);
  fs.writeFileSync(arch, sheet.renderSheet());
  fs.writeFileSync(fig, sheet.renderFigure());
  return { arch, fig };
}

function htmlPage(svg, { png }) {
  if (png) {
    return `<!doctype html><html><head><meta charset="utf-8"><style>
      html,body{margin:0;padding:0;width:5400px;height:3600px;overflow:hidden;background:#F7F4EA}
      svg{width:5400px;height:3600px;display:block}
    </style></head><body>${svg}</body></html>`;
  }
  return `<!doctype html><html><head><meta charset="utf-8"><style>
    @page{size:36in 24in;margin:0}
    html,body{margin:0;padding:0;width:36in;height:24in;overflow:hidden;background:#F7F4EA}
    svg{width:36in;height:24in;display:block}
  </style></head><body>${svg}</body></html>`;
}

function readPngSize(buf) {
  if (buf.toString('ascii', 1, 4) !== 'PNG') throw new Error('not a PNG');
  return { w: buf.readUInt32BE(16), h: buf.readUInt32BE(20) };
}

function pdfChecks(buf) {
  const text = buf.toString('latin1');
  const box = text.match(/\/MediaBox\s*\[\s*([0-9.]+)\s+([0-9.]+)\s+([0-9.]+)\s+([0-9.]+)\s*\]/);
  const fonts = (text.match(/\/Type\s*\/Font\b/g) || []).length;
  return { box: box ? box.slice(1).map(Number) : null, fonts };
}

async function rasterize(browser, svgPath, pdfPath, pngPath) {
  const svg = fs.readFileSync(svgPath, 'utf8');
  const page = await browser.newPage();
  try {
    await page.setContent(htmlPage(svg, { png: false }), { waitUntil: 'load' });
    await page.pdf({
      path: pdfPath,
      width: '36in',
      height: '24in',
      printBackground: true,
      margin: { top: 0, right: 0, bottom: 0, left: 0 },
      preferCSSPageSize: true,
    });
    await page.setViewport({ width: 5400, height: 3600, deviceScaleFactor: 1 });
    await page.setContent(htmlPage(svg, { png: true }), { waitUntil: 'load' });
    await page.screenshot({
      path: pngPath,
      type: 'png',
      clip: { x: 0, y: 0, width: 5400, height: 3600 },
      omitBackground: false,
    });
  } finally {
    await page.close();
  }
  const png = readPngSize(fs.readFileSync(pngPath));
  const pdf = pdfChecks(fs.readFileSync(pdfPath));
  if (png.w !== 5400 || png.h !== 3600) {
    throw new Error(`${path.basename(pngPath)} is ${png.w}x${png.h}, expected 5400x3600`);
  }
  if (!pdf.box || pdf.box[2] < 2590 || pdf.box[2] > 2594 || pdf.box[3] < 1726 || pdf.box[3] > 1730) {
    throw new Error(`${path.basename(pdfPath)} MediaBox ${JSON.stringify(pdf.box)}, expected about [0 0 2592 1728]`);
  }
  if (pdf.fonts !== 0) {
    throw new Error(`${path.basename(pdfPath)} embeds ${pdf.fonts} fonts; text must be outlines`);
  }
}

function run(cmd, cmdArgs, cwd) {
  return new Promise((resolve, reject) => {
    const child = spawn(cmd, cmdArgs, { cwd, shell: true, stdio: 'inherit', env: process.env });
    child.on('exit', (code) => {
      if (code === 0) resolve();
      else reject(new Error(`${cmd} exited ${code}`));
    });
  });
}

async function renderMermaid(edge) {
  fs.mkdirSync(mermaidDir, { recursive: true });
  const puppeteerConfig = path.join(process.env.TEMP || 'C:\\Users\\bcw\\AppData\\Local\\Temp', 'mps-mmdc-puppeteer.json');
  fs.writeFileSync(puppeteerConfig, JSON.stringify({ executablePath: edge, headless: true }));
  const names = ['logical', 'physical', 'cicd', 'investigation-flow', 'as-is-flow'].filter((n) => !only || n === only);
  for (const name of names) {
    const md = fs.readFileSync(path.join(here, `${name}.md`), 'utf8');
    const match = md.match(/```mermaid\r?\n([\s\S]*?)```/);
    if (!match) throw new Error(`${name}.md has no mermaid block`);
    const mmd = path.join(mermaidDir, `${name}.mmd`);
    const svg = path.join(mermaidDir, `${name}.svg`);
    fs.writeFileSync(mmd, match[1].trim() + '\n');
    await run('npx', [
      '-y',
      '-p',
      '@mermaid-js/mermaid-cli',
      'mmdc',
      '-i', mmd,
      '-o', svg,
      '-b', 'transparent',
      '-c', path.join(here, 'mermaid.config.json'),
      '-p', puppeteerConfig,
    ], repo);
  }
}

function assertOverlapCheckWorks() {
  const overlap = collectLabelIssues([
    { id: 'a', text: 'a', box: { x: 0, y: 0, w: 20, h: 10 } },
    { id: 'b', text: 'b', box: { x: 10, y: 0, w: 20, h: 10 } },
  ]);
  if (!overlap.some((line) => line.includes('text overlap'))) {
    throw new Error('overlap check did not fire on overlapping labels');
  }
  const clear = collectLabelIssues([
    { id: 'a', text: 'a', box: { x: 0, y: 0, w: 10, h: 10 }, clip: { x: -2, y: -2, w: 40, h: 20 } },
    { id: 'b', text: 'b', box: { x: 20, y: 0, w: 10, h: 10 }, clip: { x: -2, y: -2, w: 40, h: 20 } },
  ]);
  if (clear.length) throw new Error(`clear labels failed: ${clear.join('; ')}`);
  const clipped = collectLabelIssues([
    { id: 'c', text: 'c', box: { x: 0, y: 0, w: 30, h: 10 }, clip: { x: 0, y: 0, w: 10, h: 10 } },
  ]);
  if (!clipped.some((line) => line.includes('escapes its clip'))) {
    throw new Error('clip check did not fire');
  }
  const drawn = collectLabelIssues(
    [{ id: 't', text: 't', box: { x: 0, y: 0, w: 10, h: 10 } }],
    [{ id: 'ink', x: 5, y: 0, w: 10, h: 10 }],
  );
  if (!drawn.some((line) => line.includes('overlaps drawing'))) {
    throw new Error('drawing overlap check did not fire');
  }
}

function writeSlideArt(fonts) {
  if (only) return;
  const mapPath = path.join(here, 'mps-sites.svg');
  fs.writeFileSync(mapPath, drawSiteMap(fonts));
  console.log(`validated sites map: ${mapPath}`);
  const deckDir = path.join(here, '..', 'deck');
  fs.mkdirSync(deckDir, { recursive: true });
  for (const [name, svg] of drawDeckPlaceholders(fonts)) {
    const file = path.join(deckDir, name);
    fs.writeFileSync(file, svg);
    console.log(`validated placeholder: ${file}`);
  }
}

async function main() {
  assertOverlapCheckWorks();
  const parse = await loadParse();
  const fonts = loadFonts(path.join(here, 'fonts'), parse);
  writeSlideArt(fonts);
  const logo = logoInner(fs.readFileSync(path.join(repo, 'design', 'brand', 'logo-concept-1.svg'), 'utf8'));
  const selected = SHEETS.filter((s) => !only || s.slug === only);
  if (!selected.length) throw new Error(`no sheet named ${only}`);

  for (const meta of selected) {
    const sheet = build(fonts, meta, logo);
    try {
      sheet.validate();
    } catch (err) {
      const debug = path.join(process.env.TEMP || 'C:\\Users\\bcw\\AppData\\Local\\Temp', `mps-${meta.slug}-debug.svg`);
      fs.writeFileSync(debug, sheet.renderSheet());
      console.error(`\n${meta.slug} failed validation. Debug SVG: ${debug}`);
      console.error(err.message);
      process.exitCode = 1;
      return;
    }
    const written = writeSheets(sheet, meta);
    const archSvg = fs.readFileSync(written.arch, 'utf8');
    if (archSvg.includes('NaN') || archSvg.includes('Infinity')) {
      throw new Error(`${meta.slug} SVG contains a non-finite outline`);
    }
    console.log(`validated ${meta.slug}: ${written.arch}`);
  }

  if (checkOnly || process.exitCode) return;

  const edge = edgePath();
  if (!skipMermaid) await renderMermaid(edge);

  const puppeteer = await loadPuppeteer();
  const browser = await puppeteer.launch({
    executablePath: edge,
    headless: true,
    args: ['--disable-gpu', '--allow-file-access-from-files'],
  });
  try {
    fs.mkdirSync(previewDir, { recursive: true });
    for (const meta of selected) {
      const arch = path.join(printDir, `${meta.slug}-archD.svg`);
      const pdf = path.join(printDir, `${meta.slug}-archD.pdf`);
      const png = path.join(previewDir, `${meta.slug}-archD.png`);
      await rasterize(browser, arch, pdf, png);
      console.log(`printed ${meta.slug}: ${pdf}`);
    }
  } finally {
    await browser.close();
  }
}

main().catch((err) => {
  console.error(err);
  process.exitCode = 1;
});
