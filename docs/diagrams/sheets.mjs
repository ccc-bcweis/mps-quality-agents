// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company
// Hand-tuned ARCH D layouts. Mermaid auto-layout crosses these graphs.

import { INK, NAVY } from './sheet.mjs';
import { drawFabFloor } from './fab-floor.mjs';

const HAND = '#B91C1C';

function rowOf(sheet, ids, lines, x, y, w, h, gap, tone, opts = {}) {
  const n = lines.length;
  const nodeW = (w - gap * (n - 1)) / n;
  const out = [];
  lines.forEach((text, i) => {
    const id = ids[i];
    sheet.node(id, x + i * (nodeW + gap), y, nodeW, h, text, opts.tones ? opts.tones[i] : tone, opts.nodeOpts || {});
    out.push(id);
    if (opts.arrows && i > 0) sheet.link(ids[i - 1], id, 'right', 'left');
  });
  return out;
}

export function drawLogical(sheet) {
  const a = sheet.area;
  const right = a.x + a.w;

  sheet.banner(
    'thesis',
    a.x,
    a.y,
    a.w,
    46,
    'Engineers decide. AI assembles and reasons over evidence. Rules enforce. The model never holds write authority.',
  );

  const y1 = 218;
  const row1h = 400;
  const intakeW = 420;
  const detX = a.x + intakeW + 130;
  const detW = right - detX;

  sheet.zone('z-in', a.x, y1, intakeW, row1h, 'INTAKE', 'human');
  sheet.zone('z-det', detX, y1, detW, row1h, 'DETERMINISTIC — code, unit tested', 'det');

  const sX = a.x + 14;
  const sW = intakeW - 28;
  const sH = 56;
  const sY = [262, 340, 406, 472, 538];
  const sources = [
    ['Harness bar', 'Injector, reset, scenarios'],
    ['Three plants', 'Northwater, Ironwood, Lumen'],
    ['Sites API', 'List, one site, compare'],
    ['Scenario evidence', 'Script records, full text'],
    ['Notices and reports', 'Operator, NCR, supplier'],
  ];
  sources.forEach((lines, i) => sheet.node(`src${i}`, sX, sY[i], sW, sH, lines, 'human'));
  sheet.link('src0', 'src1', 'bottom', 'top');

  const fY = 266;
  const fH = 88;
  const fGap = 36;
  const fPad = 16;
  const fW = (detW - fPad * 2 - fGap * 3) / 4;
  const flows = [
    ['SPC windows', 'Western Electric rules 1-4'],
    ['Ticketing', 'Alert opens a TKT'],
    ['Intake', 'Validate, dedupe, severity'],
    ['Lifecycle', 'Only legal transitions'],
  ];
  flows.forEach((lines, i) => {
    sheet.node(`flow${i}`, detX + fPad + i * (fW + fGap), fY, fW, fH, lines, 'det');
  });
  for (let i = 0; i < 3; i += 1) sheet.link(`flow${i}`, `flow${i + 1}`, 'right', 'left');

  const busY = fY + fH + 48;
  const impactY = busY + 30;
  const impactW = fW * 2 + fGap;
  sheet.node('impact', detX + fPad, impactY, impactW, y1 + row1h - impactY - 14, [
    'Impact and sites',
    'Scope, timing, and a plant compare',
    'SC-5 lot reaches two plants',
  ], 'det');

  const sim = sheet.box('src1');
  const spc = sheet.box('flow0');
  const gx = (sim.x + sim.w + spc.x) / 2;
  sheet.arrow(
    [
      { x: sim.x + sim.w, y: sheet.cy('src1') },
      { x: gx, y: sheet.cy('src1') },
      { x: gx, y: sheet.cy('flow0') },
      { x: spc.x, y: sheet.cy('flow0') },
    ],
    { id: 'telemetry' },
  );
  sheet.pill('pill-tel', gx, 246, 'telemetry');

  const busX = sim.x + sim.w + 16;
  for (const id of ['src2', 'src3', 'src4']) {
    const n = sheet.box(id);
    sheet.arrow(
      [
        { x: n.x + n.w, y: sheet.cy(id) },
        { x: busX, y: sheet.cy(id) },
      ],
      { id: `tick-${id}`, head: false },
    );
  }
  const intake = sheet.box('flow2');
  const inCx = intake.x + intake.w / 2;
  sheet.arrow(
    [
      { x: busX, y: sheet.cy('src2') },
      { x: busX, y: busY },
      { x: inCx, y: busY },
      { x: inCx, y: intake.y + intake.h },
    ],
    { id: 'channels' },
  );
  sheet.pill('pill-ch', (busX + inCx) / 2, busY - 24, 'channels');

  const life = sheet.box('flow3');
  const channelX = 48;
  const corridorY = 634;
  sheet.pill('pill-hand', life.x + life.w / 2 + 96, 490, 'to orchestrator');

  const y2 = 650;
  const row2h = 240;
  const z2x = a.x + 32;
  const z2w = right - z2x - 12;
  sheet.zone('z-ev', z2x, y2, z2w, row2h, 'READ-ONLY EVIDENCE', 'gov');

  const chipY = 696;
  const chipH = 82;
  const systems = [
    ['MES', 'Runs, lots, recipes'],
    ['QMS', 'NCRs and CAPAs'],
    ['PLM', 'Configs, BOM, ECOs'],
    ['ERP', 'Lots and suppliers'],
    ['CMMS', 'PM and calibrations'],
    ['Ticketing', 'TKT records'],
  ];
  rowOf(
    sheet,
    systems.map((_, i) => `sys${i}`),
    systems,
    z2x + 14,
    chipY,
    z2w - 28,
    chipH,
    12,
    'gov',
  );

  const docY = 792;
  const docs = [
    ['Corpus and scenarios', 'Procedures, bulletins, and script records'],
    ['Chunk and embed', 'Heading-aware chunks, versioned index'],
    ['Hybrid retrieval', 'Keyword plus vector, family and revision filters'],
  ];
  rowOf(
    sheet,
    ['doc0', 'doc1', 'doc2'],
    docs,
    z2x + 14,
    docY,
    z2w - 28,
    78,
    16,
    'gov',
    { arrows: true },
  );

  const y3 = 922;
  const row3h = a.y + a.h - y3;
  sheet.zone('z-j', z2x, y3, z2w, row3h, 'JUDGMENT PATH', 'ai');

  const pY = 972;
  const pH = 100;
  const pGap = 28;
  const pPad = 14;
  const pW = (z2w - pPad * 2 - pGap * 5) / 6;
  const pathLines = [
    ['Orchestrator', 'Workflow behind one interface'],
    ['Investigation Agent', 'Read-only tools, at most 10 calls'],
    ['Evidence gate', 'Gaps escalate. No cause asserted.'],
    ['Resolution Agent', 'Full record text. No tools.'],
    ['Grounding validator', 'Cited ids must be in the pack'],
    ['Meridian console', 'Sites through CAPA', 'Harness bar stays outside'],
  ];
  const pathTones = ['ai', 'ai', 'det', 'ai', 'det', 'human'];
  pathLines.forEach((lines, i) => {
    sheet.node(`path${i}`, z2x + pPad + i * (pW + pGap), pY, pW, pH, lines, pathTones[i]);
  });
  for (let i = 0; i < 5; i += 1) sheet.link(`path${i}`, `path${i + 1}`, 'right', 'left');

  const orch = sheet.box('path0');
  sheet.arrow(
    [
      { x: sheet.cx('flow3'), y: life.y + life.h },
      { x: sheet.cx('flow3'), y: corridorY },
      { x: channelX, y: corridorY },
      { x: channelX, y: sheet.cy('path0') },
      { x: orch.x, y: sheet.cy('path0') },
    ],
    { id: 'handoff' },
  );

  const readX = sheet.cx('path1');
  sheet.arrow(
    [
      { x: readX, y: y2 + row2h },
      { x: readX, y: sheet.box('path1').y },
    ],
    { id: 'readonly' },
  );
  sheet.pill('pill-ro', readX + 70, (y2 + row2h + pY) / 2, 'read-only');

  const lowY = 1112;
  const gw = sheet.box('path0');
  const gwW = pW * 4 + pGap * 3;
  sheet.node('gateway', gw.x, lowY, gwW, 86, [
    'Model gateway',
    'Route, retry, breaker, and a $2 per day hard stop',
  ], 'gw');
  sheet.arrow(
    [
      { x: sheet.cx('path1'), y: pY + pH },
      { x: sheet.cx('path1'), y: lowY },
    ],
    { id: 'gw-inv' },
  );
  sheet.arrow(
    [
      { x: sheet.cx('path3'), y: pY + pH },
      { x: sheet.cx('path3'), y: lowY },
    ],
    { id: 'gw-res' },
  );

  const models = [
    ['Azure OpenAI', 'gpt-5.4-mini, one deployment'],
    ['OpenAI', 'Same gateway route'],
    ['Replay', 'Over cap, or local default'],
    ['Mock reasoner', 'No recording for this fault'],
  ];
  rowOf(sheet, ['m0', 'm1', 'm2', 'm3'], models, gw.x, 1214, gwW, 96, 12, 'gw');

  const gov = [
    ['Audit log', 'Append-only. SPC, workflow, gateway.'],
    ['Eval runner', '24 cases. Hard gates on unsafe answers.'],
    ['Red-team', 'Injection doc and operator text'],
    ['Registry', 'Prompt, model, and index versions'],
  ];
  rowOf(sheet, ['g0', 'g1', 'g2', 'g3'], gov, gw.x, 1326, gwW, 100, 12, 'gov');

  const writeX = sheet.box('path4').x;
  const writeW = pW * 2 + pGap;
  const steps = [
    ['Escalation gate', 'No bare approve'],
    ['Approval token', 'Override needs a reason'],
    ['Action service', 'CAPA write. Not an agent tool.'],
  ];
  const stepGap = 28;
  const stepW = (writeW - stepGap * 2) / 3;
  steps.forEach((lines, i) => {
    sheet.node(`step${i}`, writeX + i * (stepW + stepGap), lowY, stepW, 110, lines, 'human');
  });
  sheet.link('step0', 'step1', 'right', 'left');
  sheet.link('step1', 'step2', 'right', 'left');

  const gapY = pY + pH + 22;
  sheet.arrow(
    [
      { x: sheet.cx('path5'), y: pY + pH },
      { x: sheet.cx('path5'), y: gapY },
      { x: sheet.cx('step0'), y: gapY },
      { x: sheet.cx('step0'), y: lowY },
    ],
    { id: 'approve' },
  );

}

export function drawPhysical(sheet) {
  const a = sheet.area;
  const gap = 16;
  const idW = 430;
  const tgtW = 530;
  const rgX = a.x + idW + gap;
  const rgW = a.w - idW - tgtW - gap * 2;
  const tgtX = a.x + a.w - tgtW;
  const topH = 1048;
  const barY = a.y + topH + 14;
  const barH = a.h - topH - 14;

  sheet.zone('z-id', a.x, a.y, idW, topH, 'IDENTITY', 'human');
  sheet.zone('z-rg', rgX, a.y, rgW, topH, 'RESOURCE GROUP rg-mps-demo', 'deploy');
  sheet.zone('z-tg', tgtX, a.y, tgtW, topH, 'TARGETS', 'gw');
  sheet.zone('z-gh', a.x, barY, a.w, barH, 'GITHUB ACTIONS — ci.yml does not deploy', 'gov');

  const idX = a.x + 14;
  const idN = idW - 28;
  sheet.node('reviewer', idX, 214, idN, 100, ['Invited reviewer', 'B2B guest in the tenant'], 'human');
  sheet.node('entra', idX, 390, idN, 110, ['Entra ID app', 'Single-tenant, assignment required'], 'human');
  sheet.node('assign', idX, 530, idN, 100, ['Assignment required', 'Unassigned users get no token'], 'human');
  sheet.node('blocked', idX, 660, idN, 100, ['Uninvited blocked', 'No guest invite, no session'], 'human');
  sheet.note(
    'id-note',
    idX,
    790,
    idN,
    210,
    'Local run uses a dev user, not Entra. Hosted demo is invite-only. Managed identity is the app credential. No access keys in images.',
  );

  const swaX = rgX + 16;
  const swaW = 210;
  sheet.node('swa', swaX, 395, swaW, 110, ['Static Web App', 'Harness over console'], 'deploy');
  sheet.link('entra', 'swa', 'right', 'left');

  const acaX = swaX + swaW + 16;
  const acaW = rgX + rgW - 16 - acaX;
  const acaY = 248;
  const acaH = 290;
  sheet.roundRect(acaX, acaY, acaW, acaH, 10, {
    fill: '#F4F8FF',
    stroke: NAVY,
    'stroke-width': 1.4,
    'stroke-dasharray': '8 5',
  });
  sheet.text('aca-label', 'Container Apps environment — network boundary', acaX + 14, acaY + 22, {
    size: 14,
    weight: 'semibold',
    fill: NAVY,
  });

  const rowY = 400;
  const rowH = 100;
  const innerW = acaW - 28;
  const g3 = 24;
  const cW = (innerW - g3 * 2) / 3;
  const c0 = acaX + 14;
  sheet.node('sim', c0, acaY + 40, cW, 86, ['Simulator', 'Three plants, one seed'], 'deploy');
  sheet.node('api', c0, rowY, cW, rowH, ['API', 'Sites routes. Scale 0 to 3.', 'Escalated approve is refused'], 'deploy');
  sheet.node('worker', c0 + (cW + g3), rowY, cW, rowH, ['Worker', 'Scales on queue depth'], 'deploy');
  sheet.node('mock', c0 + 2 * (cW + g3), rowY, cW, rowH, ['Mock enterprise', 'Quality, planning, maintenance', 'Scenario evidence on read'], 'deploy');
  sheet.link('sim', 'api', 'bottom', 'top');
  sheet.link('swa', 'api', 'right', 'left');
  sheet.link('api', 'worker', 'right', 'left');
  sheet.link('worker', 'mock', 'right', 'left');

  const dataY = 620;
  const dataH = 110;
  const cols = [
    ['queue', 'Storage queue', 'API hands off the investigation'],
    ['sql', 'Azure SQL serverless', 'Incidents, audit, spend ledger'],
    ['search', 'AI Search or in-proc', 'Hybrid index, or the local file'],
    ['aoai', 'Azure OpenAI', 'gpt-5.4-mini, TPM quota'],
  ];
  const dGap = 14;
  const dW = (rgW - 32 - dGap * 3) / 4;
  cols.forEach((c, i) => {
    sheet.node(c[0], rgX + 16 + i * (dW + dGap), dataY, dW, dataH, [c[1], c[2]], 'deploy');
  });

  const busY = 568;
  sheet.arrow(
    [
      { x: sheet.cx('api'), y: rowY + rowH },
      { x: sheet.cx('api'), y: busY },
      { x: sheet.cx('queue'), y: busY },
      { x: sheet.cx('queue'), y: dataY },
    ],
    { id: 'api-queue' },
  );
  const wcx = sheet.cx('worker');
  sheet.arrow(
    [
      { x: wcx, y: rowY + rowH },
      { x: wcx, y: busY },
    ],
    { id: 'worker-bus', head: false },
  );
  for (const id of ['sql', 'search', 'aoai']) {
    sheet.arrow(
      [
        { x: wcx, y: busY },
        { x: sheet.cx(id), y: busY },
        { x: sheet.cx(id), y: dataY },
      ],
      { id: `worker-${id}` },
    );
  }

  const platY = 760;
  const plat = [
    ['kv', 'Key Vault', 'No secrets in the image'],
    ['mi', 'Managed identity', 'RBAC to SQL, OpenAI, Search, Vault'],
    ['ai', 'App Insights', 'OTel GenAI traces and alerts'],
    ['cap', 'Spend cap', '$2 per day, then replay'],
  ];
  plat.forEach((c, i) => {
    sheet.node(c[0], rgX + 16 + i * (dW + dGap), platY, dW, 120, [c[1], c[2]], i === 3 ? 'gw' : 'gov');
  });

  sheet.note(
    'rg-note',
    rgX + 16,
    910,
    rgW - 32,
    110,
    'Rollback is a Container Apps revision. Prompt and model versions roll back without a full app rollback. DR for the demo is IaC redeploy plus SQL geo-backup, configured at stand-up.',
  );

  const plantY = 1036;
  const plantH = 148;
  const plantGap = 16;
  const plantW = (rgW - 32 - plantGap * 2) / 3;
  const plants = [
    ['Northwater Fab', 'Simulated front-end fab', 'SC-5 lot starts here'],
    ['Ironwood Fab', 'Simulated front-end fab', 'Same SC-5 lot'],
    ['Lumen Pilot Works', 'Simulated strip and pilot', 'Not on that lot'],
  ];
  plants.forEach((lines, i) => {
    sheet.node(`plant${i}`, rgX + 16 + i * (plantW + plantGap), plantY, plantW, plantH, lines, 'gov');
  });

  const tX = tgtX + 14;
  const tW = tgtW - 28;
  const targets = [
    [
      { text: 'API p95', size: 14, weight: 'semibold', fill: '#111827' },
      { text: 'under 300 ms', size: 20, weight: 'bold', fill: NAVY },
      { text: 'Investigations are async', size: 14, weight: 'regular', fill: '#1F2937' },
    ],
    [
      { text: 'Investigation p95', size: 14, weight: 'semibold', fill: '#111827' },
      { text: 'under 45 s', size: 20, weight: 'bold', fill: NAVY },
      { text: 'Tools plus two model calls', size: 14, weight: 'regular', fill: '#1F2937' },
    ],
    [
      { text: 'Unit cost', size: 14, weight: 'semibold', fill: '#111827' },
      { text: '$0.02 to $0.10', size: 20, weight: 'bold', fill: NAVY },
      { text: 'Per successful investigation', size: 14, weight: 'regular', fill: '#1F2937' },
    ],
    [
      { text: 'Hard cap', size: 14, weight: 'semibold', fill: '#111827' },
      { text: '$2 per day', size: 20, weight: 'bold', fill: NAVY },
      { text: 'Over cap switches to replay', size: 14, weight: 'regular', fill: '#1F2937' },
    ],
    [
      { text: 'Scale', size: 14, weight: 'semibold', fill: '#111827' },
      { text: 'API 0-3', size: 20, weight: 'bold', fill: NAVY },
      { text: 'Worker on queue. Simulator x1', size: 14, weight: 'regular', fill: '#1F2937' },
    ],
    [
      { text: 'Not this drawing', size: 14, weight: 'semibold', fill: '#111827' },
      { text: 'docker compose', size: 18, weight: 'bold', fill: NAVY },
      { text: 'API, mocks, web, Aspire', size: 14, weight: 'regular', fill: '#1F2937' },
    ],
    [
      { text: 'Talk track only', size: 14, weight: 'semibold', fill: '#111827' },
      { text: 'Front Door + WAF', size: 18, weight: 'bold', fill: NAVY },
      { text: 'Private endpoints, Event Hubs', size: 14, weight: 'regular', fill: '#1F2937' },
    ],
  ];
  const tH = 118;
  const tGap = 14;
  targets.forEach((lines, i) => {
    sheet.node(`t${i}`, tX, 214 + i * (tH + tGap), tW, tH, lines, 'gw');
  });

  const gX = a.x + 16;
  const gY = barY + 48;
  const gH = barH - 64;
  const gGap = 18;
  const gW = (a.w - 32 - gGap * 2) / 3;
  sheet.node('gh-ci', gX, gY, gW, gH, [
    'ci.yml',
    'Build, test, coverage, format, CodeQL, gitleaks, dependency review, Bicep build',
  ], 'gov');
  sheet.node('gh-cd', gX + gW + gGap, gY, gW, gH, [
    'deploy.yml',
    'Manual only. Environment azure-demo. Required reviewer. Bicep what-if, then deploy.',
  ], 'review');
  sheet.node('gh-oidc', gX + 2 * (gW + gGap), gY, gW, gH, [
    'OIDC to Azure',
    'No stored cloud secrets. Push images to ACR. New Container Apps revision.',
  ], 'ok');
}

export function drawCicd(sheet) {
  const a = sheet.area;
  const top = a.y + 40;
  const headers = [
    ['LANES', 0],
    ['PR', 1],
    ['CI CHECKS', 2],
    ['REVIEW', 3],
    ['MAIN', 4],
    ['DEPLOY', 5],
    ['SMOKE', 6],
  ];
  const widths = [430, 34, 156, 34, 470, 34, 250, 30, 190, 30, 400, 28];
  const fixed = widths.reduce((s, n) => s + n, 0);
  const smokeW = a.w - fixed;
  const xs = [];
  let cursor = a.x;
  const keys = ['lanes', 'g1', 'pr', 'g2', 'ci', 'g3', 'review', 'g4', 'main', 'g5', 'deploy', 'g6', 'smoke'];
  const allW = [...widths, smokeW];
  keys.forEach((key, i) => {
    xs.push({ key, x: cursor, w: allW[i] });
    cursor += allW[i];
  });
  const col = (key) => xs.find((c) => c.key === key);

  headers.forEach(([label], i) => {
    const key = ['lanes', 'pr', 'ci', 'review', 'main', 'deploy', 'smoke'][i];
    const c = col(key);
    sheet.text(`col-${key}`, label, c.x, a.y + 22, { size: 14, weight: 'bold', fill: NAVY });
  });

  const laneLines = [
    ['GB-F Foundation', 'lane/f-foundation · P0'],
    ['GB-D Design', 'lane/d-design · P0'],
    ['GB-1 Data and simulation', 'lane/1-data-sim'],
    ['GB-2 Agents', 'lane/2-agents'],
    ['GB-3 API', 'lane/3-api'],
    ['GB-4 UI', 'lane/4-ui'],
    ['GB-5 QA, eval, security', 'lane/5-qa'],
  ];
  const laneH = 92;
  const laneGap = 18;
  const laneCol = col('lanes');
  laneLines.forEach((lines, i) => {
    sheet.node(`lane${i}`, laneCol.x, top + i * (laneH + laneGap), laneCol.w, laneH, lines, 'lane');
  });
  const laneEnd = top + laneLines.length * laneH + (laneLines.length - 1) * laneGap;
  sheet.note(
    'lane-note',
    laneCol.x,
    laneEnd + 24,
    laneCol.w,
    130,
    'P0: GB-F merges first so contracts freeze. P1 runs at most five Grok Build sessions. Agents do not call git remotes. Bob syncs with sync_to_github.ps1.',
  );

  const laneMid = top + (laneLines.length * laneH + (laneLines.length - 1) * laneGap) / 2;
  const prCol = col('pr');
  const prH = 140;
  sheet.node('pr', prCol.x, laneMid - prH / 2, prCol.w, prH, ['Pull request', 'One lane, one PR'], 'neutral');

  const busX = laneCol.x + laneCol.w + 16;
  const firstCy = sheet.cy('lane0');
  const lastCy = sheet.cy(`lane${laneLines.length - 1}`);
  sheet.arrow(
    [
      { x: busX, y: firstCy },
      { x: busX, y: lastCy },
    ],
    { id: 'lane-bus', head: false },
  );
  laneLines.forEach((_, i) => {
    const n = sheet.box(`lane${i}`);
    sheet.arrow(
      [
        { x: n.x + n.w, y: sheet.cy(`lane${i}`) },
        { x: busX, y: sheet.cy(`lane${i}`) },
      ],
      { id: `lane-tick-${i}`, head: false },
    );
  });
  sheet.arrow(
    [
      { x: busX, y: sheet.cy('pr') },
      { x: sheet.box('pr').x, y: sheet.cy('pr') },
    ],
    { id: 'bus-pr' },
  );

  const checks = [
    ['Build', 'Warnings as errors, analyzers', false],
    ['Test', 'xUnit, Vitest, ESLint, tsc', false],
    ['Coverage', '70% Domain and Application', false],
    ['Format', 'dotnet format --verify-no-changes', false],
    ['CodeQL', 'C# and JavaScript', false],
    ['gitleaks', 'Fails the PR if a secret is committed', false],
    ['Dependency review', 'New vulnerable dependencies', false],
    ['Bicep build', 'Compile only. Does not deploy.', false],
    ['Eval replay', 'After I1. Hard gates.', true],
    ['Red-team and ZAP', 'After I1. Injection and baseline.', true],
  ];
  const ciCol = col('ci');
  const ciH = 96;
  const ciGap = 22;
  checks.forEach((c, i) => {
    sheet.node(
      `ci${i}`,
      ciCol.x,
      top + i * (ciH + ciGap),
      ciCol.w,
      ciH,
      [c[0], c[1]],
      c[2] ? 'later' : 'ok',
    );
  });
  const ciBus = ciCol.x - 16;
  sheet.arrow(
    [
      { x: ciBus, y: sheet.cy('ci0') },
      { x: ciBus, y: sheet.cy('ci9') },
    ],
    { id: 'ci-bus', head: false },
  );
  checks.forEach((c, i) => {
    sheet.arrow(
      [
        { x: ciBus, y: sheet.cy(`ci${i}`) },
        { x: ciCol.x, y: sheet.cy(`ci${i}`) },
      ],
      { id: `ci-tick-${i}`, head: true, dashed: c[2] },
    );
  });
  sheet.arrow(
    [
      { x: sheet.box('pr').x + sheet.box('pr').w, y: sheet.cy('pr') },
      { x: ciBus, y: sheet.cy('pr') },
    ],
    { id: 'pr-ci' },
  );

  const revCol = col('review');
  const bobY = sheet.cy('ci3') - 90;
  sheet.node('bob', revCol.x, bobY, revCol.w, 170, [
    'Bob',
    'Required PR review',
    'Merge approver',
  ], 'review');
  sheet.node('bryan', revCol.x, bobY + 194, revCol.w, 170, [
    'Bryan',
    'Azure deploy go',
    'Does not approve merges',
  ], 'review');

  const ciOut = ciCol.x + ciCol.w + 16;
  sheet.arrow(
    [
      { x: ciOut, y: sheet.cy('ci0') },
      { x: ciOut, y: sheet.cy('ci9') },
    ],
    { id: 'ci-out-bus', head: false },
  );
  checks.forEach((_, i) => {
    const n = sheet.box(`ci${i}`);
    sheet.arrow(
      [
        { x: n.x + n.w, y: sheet.cy(`ci${i}`) },
        { x: ciOut, y: sheet.cy(`ci${i}`) },
      ],
      { id: `ci-out-${i}`, head: false },
    );
  });
  sheet.arrow(
    [
      { x: ciOut, y: sheet.cy('bob') },
      { x: sheet.box('bob').x, y: sheet.cy('bob') },
    ],
    { id: 'ci-bob' },
  );

  const mainCol = col('main');
  const mainH = 160;
  sheet.node('main', mainCol.x, sheet.cy('bob') - mainH / 2, mainCol.w, mainH, [
    'main',
    'Protected branch',
  ], 'neutral');
  sheet.link('bob', 'main', 'right', 'left');

  const depCol = col('deploy');
  const depH = 96;
  const depGap = 22;
  const deps = [
    ['Manual trigger', 'workflow_dispatch only'],
    ['Environment', 'azure-demo'],
    ['Required reviewer', 'Deploy stays blocked'],
    ['OIDC to Azure', 'No stored cloud secrets'],
    ['Build and push', 'Images to the registry'],
    ['Bicep what-if', 'Review the change first'],
    ['Deploy', 'Resource group rg-mps-demo'],
  ];
  deps.forEach((lines, i) => {
    sheet.node(`dep${i}`, depCol.x, top + i * (depH + depGap), depCol.w, depH, lines, 'deploy');
    if (i > 0) sheet.link(`dep${i - 1}`, `dep${i}`, 'bottom', 'top');
  });

  const depApproach = depCol.x - 22;
  sheet.arrow(
    [
      { x: mainCol.x + mainCol.w, y: sheet.cy('main') },
      { x: depApproach, y: sheet.cy('main') },
      { x: depApproach, y: sheet.cy('dep0') },
      { x: depCol.x, y: sheet.cy('dep0') },
    ],
    { id: 'main-dep' },
  );

  const smokeCol = col('smoke');
  sheet.node('smoke', smokeCol.x, top, smokeCol.w, 320, [
    'Smoke',
    'Invited guest signs in',
    'LIVE badge and spend meter',
    'S1 in replay, then live',
    'Uninvited user is blocked',
  ], 'human');
  const smokeApproach = smokeCol.x - 22;
  sheet.arrow(
    [
      { x: depCol.x + depCol.w, y: sheet.cy('dep6') },
      { x: smokeApproach, y: sheet.cy('dep6') },
      { x: smokeApproach, y: sheet.cy('smoke') },
      { x: smokeCol.x, y: sheet.cy('smoke') },
    ],
    { id: 'dep-smoke' },
  );
}

const ESCALATE = '#C2410C';

function handoff(sheet, fromId, toId, id, opts = {}) {
  const a = sheet.anchor(fromId, 'right');
  const b = sheet.anchor(toId, 'left');
  const last = 18;
  if (Math.abs(a.y - b.y) < 0.8) {
    sheet.arrow([a, b], { id, dashed: opts.dashed, color: opts.color });
    return;
  }
  const elbowX = b.x - last;
  if (elbowX <= a.x + 1) sheet.errors.push(`${id}: gutter is too narrow for the arrow head`);
  sheet.arrow([a, { x: elbowX, y: a.y }, { x: elbowX, y: b.y }, b], {
    id,
    dashed: opts.dashed,
    color: opts.color,
  });
}

export function drawInvestigation(sheet) {
  const a = sheet.area;
  const lanesTop = a.y + 36;
  const laneH = 140;
  const laneGap = 6;
  const contentX = a.x + 204;
  const gap = 24;
  const inset = 4;
  const widths = [210, 250, 200, 220, 250, 210, 240, 246, 246];
  const cols = [];
  let cursor = contentX;
  widths.forEach((w) => {
    cols.push({ x: cursor, w });
    cursor += w + gap;
  });

  const laneMeta = [
    ['L-human', 'Engineer / Human', 'human', ''],
    ['L-console', 'Console screen', 'neutral', 'Not the harness bar'],
    ['L-det', 'Deterministic services', 'det', ''],
    ['L-inv', 'Investigation Agent', 'ai', ''],
    ['L-res', 'Resolution Agent', 'ai', ''],
    ['L-tools', 'Tools / enterprise systems', 'gov', ''],
    ['L-gw', 'Model gateway', 'gw', ''],
  ];
  const lanes = laneMeta.map((meta, i) => {
    const y = lanesTop + i * (laneH + laneGap);
    return sheet.band(meta[0], a.x, y, a.w, laneH, meta[1], meta[2], meta[3]);
  });

  const span = (from, to) => {
    const x = cols[from].x;
    const right = cols[to].x + cols[to].w;
    return { x, w: right - x };
  };
  const head = (id, text, from, to) => {
    const box = span(from, to);
    sheet.text(id, text, box.x + box.w / 2, a.y + 22, {
      size: 16,
      weight: 'bold',
      fill: NAVY,
      align: 'center',
    });
  };
  head('stage-alerts', 'ALERTS / TICKETS', 0, 0);
  head('stage-inv', 'INVESTIGATION', 1, 6);
  head('stage-appr', 'APPROVAL', 7, 7);
  head('stage-capa', 'CAPA', 8, 8);

  const put = (id, col, lane, h, lines, tone, opts = {}) => {
    const c = cols[col];
    const row = lanes[lane];
    const y = row.y + (row.h - h) / 2;
    sheet.node(id, c.x + inset, y, c.w - inset * 2, h, lines, tone, opts);
  };

  const plain = 100;
  const agent = 118;
  const tall = 126;

  put('spc', 0, 2, 112, ['SPC alert', 'Statistical process control', 'Western Electric rules', 'Opens a ticket'], 'det');
  put('alerts', 0, 1, plain, ['Alerts, then Tickets', 'Plant is on the ticket', 'Lifecycle bar moves'], 'neutral');
  put('open', 0, 0, plain, ['Opens the ticket', 'Starts from the schematic', 'No write yet'], 'human');

  put('schematic', 1, 1, tall, [
    'Tool schematic',
    'Evidence entry point',
    'Chamber, gas, vacuum, chuck',
    'Radio-frequency source',
    'A sensor opens its chart',
  ], 'neutral');
  put('triage', 1, 3, agent, ['Triage', 'Plans the pack', 'Does not diagnose'], 'ai', { badge: 'AGENT' });
  put('gw-triage', 1, 6, plain, ['Model call', 'Triage route', 'Ledger and the cap'], 'gw');

  put('hypo', 2, 3, agent, ['Hypotheses', 'Names four classes', 'Does not pick one'], 'ai', { badge: 'AGENT' });

  put('calls', 3, 3, agent, ['Tool calls', 'Read-only, ten calls', 'Impact scope included'], 'ai', { badge: 'AGENT' });
  put('reads', 3, 5, plain, ['Enterprise reads', 'MES, QMS, PLM, ERP', 'Scenario text included'], 'gov');

  put('why', 4, 3, agent, ['Why it matters', 'One plain line per item', 'Written on the evidence'], 'ai', { badge: 'AGENT' });
  put('gate', 4, 2, plain, ['Evidence gate', 'Sufficient, continue', 'Else stop and list gaps'], 'det');
  put('esc-ui', 4, 1, plain, ['Request info', 'Missing or contradictory', 'Named gaps on screen'], 'neutral');
  put('esc-human', 4, 0, plain, ['No root cause', 'The engineer owns the gap', 'Nothing is asserted'], 'human');

  put('rank', 5, 4, agent, ['Rank causes', 'Sees the full record', 'Writes the matrix below'], 'ai', { badge: 'AGENT' });

  put('gw-rank', 5, 6, plain, ['Model call', 'Ranking route', 'At most two calls'], 'gw');

  put('draft', 6, 4, agent, ['CAPA draft', 'Actions, not a write', 'Same agent output'], 'ai', { badge: 'AGENT' });
  put('ground', 6, 2, plain, ['Grounding check', 'Why-line cites a real item', 'Rejects a missing item'], 'det');

  put('appr-ui', 7, 1, plain, ['Escalation gate', 'Approve stays disabled', 'Request info, or justify'], 'neutral');
  put('appr', 7, 0, agent, ['Human still decides', 'Override needs a reason', 'Then a signed token'], 'human', { badge: 'HUMAN' });

  put('act', 8, 2, plain, ['Action service', 'Token required', 'Not an agent tool'], 'det');
  put('qms', 8, 5, plain, ['Quality system', 'Creates the record', '500, timeout, or 409'], 'gov');
  put('capa-ui', 8, 1, plain, ['CAPA screen', 'Corrective and preventive action', 'Lifecycle advances'], 'neutral');

  const up = (fromId, toId, id, opts = {}) => sheet.link(fromId, toId, 'top', 'bottom', { id, ...opts });
  const down = (fromId, toId, id, opts = {}) => sheet.link(fromId, toId, 'bottom', 'top', { id, ...opts });

  up('spc', 'alerts', 'spc-ui');
  up('alerts', 'open', 'ui-open');
  handoff(sheet, 'open', 'schematic', 'open-schematic');
  down('schematic', 'triage', 'schematic-triage');
  down('triage', 'gw-triage', 'triage-gw');
  handoff(sheet, 'triage', 'hypo', 'triage-hypo');
  handoff(sheet, 'hypo', 'calls', 'hypo-calls');
  down('calls', 'reads', 'calls-reads');
  handoff(sheet, 'reads', 'why', 'reads-why');
  up('why', 'gate', 'why-gate');
  up('gate', 'esc-ui', 'gate-esc', { dashed: true, color: ESCALATE });
  up('esc-ui', 'esc-human', 'esc-human-up', { dashed: true, color: ESCALATE });
  handoff(sheet, 'gate', 'rank', 'gate-rank');
  down('rank', 'gw-rank', 'rank-gw');
  handoff(sheet, 'rank', 'draft', 'rank-draft');
  up('draft', 'ground', 'draft-ground');
  handoff(sheet, 'ground', 'appr-ui', 'ground-ui');
  up('appr-ui', 'appr', 'ui-appr');
  handoff(sheet, 'appr', 'act', 'appr-act');
  up('act', 'capa-ui', 'act-ui');
  down('act', 'qms', 'act-qms');

  // The matrix is wider than a swimlane cell. The arrow drops through the empty
  // column under Rank, then runs in the gap under the lanes so it does not cross a card.
  const lanesBottom = lanes[lanes.length - 1].y + lanes[lanes.length - 1].h;
  const panelGap = 16;
  const glossW = 640;
  const panelY = lanesBottom + 46;
  const panelH = a.y + a.h - panelY - 4;
  const matrixX = contentX;
  const matrixW = a.x + a.w - contentX - glossW - panelGap;
  const glossX = matrixX + matrixW + panelGap;
  drawEvidenceMatrix(sheet, matrixX, panelY, matrixW, panelH);
  drawFlowGlossary(sheet, glossX, panelY, glossW, panelH);

  const rankBottom = sheet.anchor('rank', 'bottom');
  const rankLeft = sheet.anchor('rank', 'left');
  const matrix = sheet.box('matrix');
  // Leave from the bottom-left corner, clear of the gateway arrow under the card,
  // drop in the gutter, then step sideways onto the rule-out column.
  const dropX = rankLeft.x - 28;
  const landY = matrix.y - 22;
  const tipX = matrix.x + 960;
  sheet.arrow(
    [
      { x: rankLeft.x, y: rankBottom.y },
      { x: rankLeft.x, y: rankBottom.y + 8 },
      { x: dropX, y: rankBottom.y + 8 },
      { x: dropX, y: landY },
      { x: tipX, y: landY },
      { x: tipX, y: matrix.y },
    ],
    { id: 'rank-matrix' },
  );
}

function drawEvidenceMatrix(sheet, x, y, w, h) {
  const box = sheet.frame('matrix', x, y, w, h, 'ai');
  const clip = { x, y, w, h };
  const classX = x + 28;
  const supportX = x + 280;
  const ruleX = x + 820;
  sheet.text('mx-title', 'Evidence matrix', classX, y + 26, {
    size: 16,
    weight: 'bold',
    fill: NAVY,
    clip,
  });
  sheet.text('mx-dek1', 'Produced at Rank causes, by the Resolution Agent.', classX, y + 50, {
    size: 14,
    weight: 'regular',
    fill: INK,
    clip,
  });
  sheet.text('mx-dek2', 'Why-lines are written earlier, on each evidence item.', classX, y + 72, {
    size: 14,
    weight: 'regular',
    fill: INK,
    clip,
  });
  const headY = y + 98;
  sheet.text('mx-h-class', 'Root-cause class', classX, headY, { size: 14, weight: 'semibold', fill: NAVY, clip });
  sheet.text('mx-h-support', 'Signals that support it', supportX, headY, { size: 14, weight: 'semibold', fill: NAVY, clip });
  sheet.text('mx-h-rule', 'Signals that rule it out', ruleX, headY, { size: 14, weight: 'semibold', fill: NAVY, clip });
  const rows = [
    ['Engineering change', 'A change order just before the shift, and only those tools moved', 'Maintenance the same week'],
    ['Out of spec', 'Overdue calibration or alignment, on one chamber', 'A new material lot that week'],
    ['Supply-chain material', 'A new lot of gas, chemical, or wafers on every tool that used it', 'A change order on one of those tools'],
    ['Process drift', 'A slow ramp that tracks hours since the chamber was cleaned', 'Ordinary seasonal noise'],
  ];
  rows.forEach((row, i) => {
    const baseline = y + 126 + i * 26;
    sheet.text(`mx-c${i}`, row[0], classX, baseline, { size: 16, weight: 'semibold', fill: INK, clip });
    sheet.text(`mx-s${i}`, row[1], supportX, baseline, { size: 16, weight: 'regular', fill: INK, clip });
    sheet.text(`mx-r${i}`, row[2], ruleX, baseline, { size: 16, weight: 'regular', fill: INK, clip });
  });
  return box;
}

function drawFlowGlossary(sheet, x, y, w, h) {
  const box = sheet.frame('glossary', x, y, w, h, 'neutral');
  const clip = { x, y, w, h };
  const left = x + 28;
  sheet.text('gl-title', 'Glossary', left, y + 26, { size: 16, weight: 'bold', fill: NAVY, clip });
  sheet.text('gl-dek', 'Console screens spell these out on first use.', left, y + 50, {
    size: 14,
    weight: 'regular',
    fill: INK,
    clip,
  });
  const terms = [
    ['SPC    statistical process control', 'PLM    product lifecycle management'],
    ['CAPA   corrective and preventive action', 'ERP    enterprise resource planning'],
    ['MES    manufacturing execution system', 'CMMS   maintenance system for work orders'],
    ['QMS    quality management system', 'Gate refuses a bare approve'],
  ];
  const col2 = x + 28 + 320;
  terms.forEach((pair, i) => {
    const baseline = y + 82 + i * 28;
    sheet.text(`gl-a${i}`, pair[0], left, baseline, { size: 14, weight: 'regular', fill: INK, clip });
    if (pair[1]) sheet.text(`gl-b${i}`, pair[1], col2, baseline, { size: 14, weight: 'regular', fill: INK, clip });
  });
  return box;
}

function stageStrip(sheet, x, y, w, h, id, title, duration) {
  sheet.roundRect(x, y, w, h, 6, { fill: '#E8EEF6', stroke: NAVY, 'stroke-width': 1.2 });
  const cx = x + w / 2;
  const clip = { x, y, w, h };
  sheet.text(`${id}-t`, title, cx, y + 24, {
    size: 16,
    weight: 'bold',
    fill: NAVY,
    align: 'center',
    clip,
  });
  sheet.text(`${id}-d`, duration, cx, y + 48, {
    size: 14,
    weight: 'semibold',
    fill: INK,
    align: 'center',
    clip,
  });
  sheet.text(`${id}-s`, 'illustrative / simulated', cx, y + 72, {
    size: 14,
    weight: 'regular',
    fill: INK,
    align: 'center',
    clip,
  });
}

function manual(n) {
  return { label: `MANUAL ${n}`, tone: 'manual' };
}

function risk(n) {
  return { label: `ERROR RISK ${n}`, tone: 'risk' };
}

export function drawAsIs(sheet) {
  const a = sheet.area;
  const labelW = 160;
  const contentX = a.x + labelW;
  const stageGap = 16;
  const innerGap = 14;
  const normal = 320;
  const sub = normal - innerGap / 2;
  const stageH = 86;
  const panelH = 252;
  const panelY = a.y + a.h - panelH;
  const lanesTop = a.y + stageH + 10;
  const laneGap = 8;
  const laneCount = 6;
  const lanesBottom = panelY - 12;
  const laneH = (lanesBottom - lanesTop - laneGap * (laneCount - 1)) / laneCount;
  const cardH = laneH - 18;
  const inset = 4;

  const xDetect = contentX;
  const xTriage = xDetect + normal + stageGap;
  const xGather = xTriage + normal + stageGap;
  const xMeet = xGather + sub + innerGap;
  const xDiag = xMeet + sub + stageGap;
  const xDecide = xDiag + normal + stageGap;
  const xCapa = xDecide + normal + stageGap;
  const cols = [
    { x: xDetect, w: normal },
    { x: xTriage, w: normal },
    { x: xGather, w: sub },
    { x: xMeet, w: sub },
    { x: xDiag, w: normal },
    { x: xDecide, w: normal },
    { x: xCapa, w: normal },
  ];

  stageStrip(sheet, xDetect, a.y, normal, stageH, 'st-det', 'DETECT', '0.5 day');
  stageStrip(sheet, xTriage, a.y, normal, stageH, 'st-tri', 'TRIAGE', '0.5 day');
  stageStrip(sheet, xGather, a.y, sub + innerGap + sub, stageH, 'st-gth', 'GATHER EVIDENCE', '2.0 days');
  stageStrip(sheet, xDiag, a.y, normal, stageH, 'st-dia', 'DIAGNOSE', '1.0 day');
  stageStrip(sheet, xDecide, a.y, normal, stageH, 'st-dec', 'DECIDE / APPROVE', '2.0 days');
  stageStrip(sheet, xCapa, a.y, normal, stageH, 'st-capa', 'CAPA', '62 days');

  const laneMeta = [
    ['L-op', 'Operator', 'human'],
    ['L-tech', 'Technician', 'review'],
    ['L-pe', 'Process engineer', 'deploy'],
    ['L-qe', 'Quality engineer', 'gov'],
    ['L-sc', 'Supply chain', 'gw'],
    ['L-mgmt', 'Management', 'ok'],
  ];
  const lanes = laneMeta.map((meta, i) => {
    const y = lanesTop + i * (laneH + laneGap);
    return sheet.band(meta[0], a.x, y, a.w, laneH, meta[1], meta[2]);
  });

  const put = (id, col, lane, lines, tone, badges) => {
    const c = cols[col];
    const row = lanes[lane];
    const y = row.y + (row.h - cardH) / 2;
    const opts = badges && badges.length ? { badges } : {};
    sheet.node(id, c.x + inset, y, c.w - inset * 2, cardH, lines, tone, opts);
  };

  put('d-op', 0, 0, ['Alarm noticed', 'MX-400 etch hits a limit', 'Notes chamber and recipe'], 'human');
  put(
    'd-tech',
    0,
    1,
    ['Told in person', 'Repeats the chamber', 'Nothing is written yet'],
    'review',
    [manual(1), risk(1)],
  );
  put(
    't-tech',
    1,
    1,
    ['Types the ticket', 'Tool, time, and symptom', 'Email is the record'],
    'review',
    [manual(2), risk(2)],
  );
  put(
    't-qe',
    1,
    3,
    ['Assigns an owner', 'Reply-all on the mail', 'Recipe step often dropped'],
    'gov',
    [manual(3), risk(3)],
  );
  put(
    'g-tech',
    2,
    1,
    ['MES spreadsheet', 'Runs, lots, and recipe', 'Exported, then emailed'],
    'review',
    [manual(4), risk(4)],
  );
  put(
    'g-pe',
    2,
    2,
    ['Historian, SPC, ECN', 'Three files, copied out', 'Change log checked by eye'],
    'deploy',
    [manual(5), risk(5)],
  );
  put(
    'g-qe',
    2,
    3,
    ['Prior cases', 'Pulled from a folder', 'Forwarded into the thread'],
    'gov',
    [manual(6), risk(6)],
  );
  put(
    'g-sc',
    2,
    4,
    ['Supplier lots', 'Looked up, then emailed', 'Not joined to the run'],
    'gw',
    [manual(7), risk(7)],
  );
  put(
    'g-meet',
    3,
    2,
    ['Emails and a meeting', 'Spreadsheets compared live', 'No single evidence pack'],
    'deploy',
    [manual(8), risk(8)],
  );
  put(
    'n-pe',
    4,
    2,
    ['Tribal hypothesis', 'Last alarm was the chamber', 'Stated from memory'],
    'deploy',
    [manual(9), risk(9)],
  );
  put(
    'a-qe',
    5,
    3,
    ['Asks for approval', 'Email to management', 'No evidence pack attached'],
    'gov',
    [manual(10)],
  );
  put(
    'a-mgmt',
    5,
    5,
    ['Approves by reply', 'The meeting is the signature', 'The cause is not cited'],
    'ok',
    [manual(11), risk(10)],
  );
  put(
    'c-qe',
    6,
    3,
    ['CAPA written by hand', 'Re-keyed into the QMS', 'Narrative, lot, and actions'],
    'gov',
    [manual(12), risk(11)],
  );
  put(
    'c-mgmt',
    6,
    5,
    ['Signs the CAPA', 'Paper or a workflow click', 'Does not see the files'],
    'ok',
    [manual(13)],
  );

  const down = (fromId, toId, id) => sheet.link(fromId, toId, 'bottom', 'top', { id, color: HAND });
  const right = (fromId, toId, id) => sheet.link(fromId, toId, 'right', 'left', { id, color: HAND });
  const upTo = (fromId, toId, id) => sheet.link(fromId, toId, 'top', 'bottom', { id, color: HAND });

  down('d-op', 'd-tech', 'op-tech');
  right('d-tech', 't-tech', 'tech-ticket');
  down('t-tech', 't-qe', 'ticket-owner');
  right('t-tech', 'g-tech', 'ticket-mes');
  right('t-qe', 'g-qe', 'owner-cases');
  upTo('g-sc', 'g-qe', 'lots-cases');
  right('g-pe', 'g-meet', 'files-meet');

  const techR = sheet.anchor('g-tech', 'right');
  const meetTop = sheet.anchor('g-meet', 'top');
  sheet.arrow([techR, { x: meetTop.x, y: techR.y }, meetTop], { id: 'mes-meet', color: HAND });

  const qeR = sheet.anchor('g-qe', 'right');
  const meetBot = sheet.anchor('g-meet', 'bottom');
  sheet.arrow([qeR, { x: meetBot.x, y: qeR.y }, meetBot], { id: 'cases-meet', color: HAND });

  right('g-meet', 'n-pe', 'meet-hyp');
  handoff(sheet, 'n-pe', 'a-qe', 'hyp-ask', { color: HAND });
  down('a-qe', 'a-mgmt', 'ask-approve');
  handoff(sheet, 'a-mgmt', 'c-qe', 'approve-rekey', { color: HAND });
  down('c-qe', 'c-mgmt', 'rekey-sign');

  const glossW = 700;
  const glossX = a.x + a.w - glossW;
  drawAsIsCallouts(sheet, a.x, panelY, glossX - a.x - 16, panelH);
  drawAsIsGlossary(sheet, glossX, panelY, glossW, panelH);
}

function drawAsIsCallouts(sheet, x, y, w, h) {
  const box = sheet.frame('callouts', x, y, w, h, 'risk');
  const clip = { x, y, w, h };
  const left = x + 24;
  const mid = x + 24 + (w - 48) / 2;
  sheet.text('co-title', 'ERROR RISK callouts', left, y + 26, {
    size: 16,
    weight: 'bold',
    fill: NAVY,
    clip,
  });
  sheet.text('co-dek1', '13 manual hand-offs. 11 places a fact can change. Illustrative / simulated.', left, y + 50, {
    size: 14,
    weight: 'regular',
    fill: INK,
    clip,
  });
  sheet.text(
    'co-dek2',
    'Detect through Diagnose is 4.0 days to a hypothesis, simulated. CAPA at 62 days is the record cycle, simulated.',
    left,
    y + 74,
    { size: 14, weight: 'regular', fill: INK, clip },
  );
  const leftLines = [
    'E1   Spoken alarm. The chamber is written from memory.',
    'E2   Ticket retyped. Tool, time, and symptom change.',
    'E3   Data gap. The reply drops the recipe step.',
    'E4   Run sheet pasted into a spreadsheet.',
    'E5   Missed engineering change. The log is checked by eye.',
    'E6   Data gap. Prior cases sit in a folder.',
  ];
  const rightLines = [
    'E7   Wrong lot traced. The supplier lot is not joined to the run.',
    'E8   Data gap. The meeting does not record which file was current.',
    'E9   Bias toward the last known cause.',
    'E10  Uncited approval. The meeting has no evidence pack.',
    'E11  Re-keyed record. The files stay in email.',
  ];
  leftLines.forEach((text, i) => {
    sheet.text(`co-l${i}`, text, left, y + 108 + i * 24, { size: 14, weight: 'regular', fill: INK, clip });
  });
  rightLines.forEach((text, i) => {
    sheet.text(`co-r${i}`, text, mid, y + 108 + i * 24, { size: 14, weight: 'regular', fill: INK, clip });
  });
  return box;
}

function drawAsIsGlossary(sheet, x, y, w, h) {
  const box = sheet.frame('glossary', x, y, w, h, 'neutral');
  const clip = { x, y, w, h };
  const left = x + 24;
  sheet.text('gl-title', 'Glossary', left, y + 26, { size: 16, weight: 'bold', fill: NAVY, clip });
  sheet.text('gl-dek', 'Short forms on this sheet, spelled out.', left, y + 50, {
    size: 14,
    weight: 'regular',
    fill: INK,
    clip,
  });
  const terms = [
    'MES    manufacturing execution system',
    'SPC    statistical process control',
    'ECN    engineering change notice',
    'QMS    quality management system',
    'CAPA   corrective and preventive action',
  ];
  terms.forEach((text, i) => {
    sheet.text(`gl-t${i}`, text, left, y + 86 + i * 28, { size: 14, weight: 'regular', fill: INK, clip });
  });
  return box;
}

function agentBadge(n) {
  return { label: `AGENT ${n}`, tone: 'ai' };
}

function humanBadge(n) {
  return { label: `HUMAN ${n}`, tone: 'human' };
}

function removed(n) {
  return { label: `ERROR REMOVED E${n}`, tone: 'removed' };
}

const FLOW = '#6D28D9';

export function drawToBe(sheet) {
  const a = sheet.area;
  const labelW = 160;
  const contentX = a.x + labelW;
  const colW = 276;
  const colGap = 16;
  const stageH = 86;
  const panelH = 252;
  const panelY = a.y + a.h - panelH;
  const lanesTop = a.y + stageH + 10;
  const lanesBottom = panelY - 12;
  const laneGap = 8;
  const heights = [100, 87, 100, 128, 87, 128, 276];
  const stack = heights.reduce((sum, h) => sum + h, 0) + laneGap * (heights.length - 1);
  if (Math.abs(stack - (lanesBottom - lanesTop)) > 0.5) {
    sheet.errors.push(`lane stack ${stack.toFixed(1)} != ${(lanesBottom - lanesTop).toFixed(1)}`);
  }

  const cols = [];
  for (let i = 0; i < 8; i += 1) {
    cols.push({ x: contentX + i * (colW + colGap), w: colW });
  }
  const end = cols[7].x + cols[7].w;
  if (Math.abs(end - (a.x + a.w)) > 0.5) {
    sheet.errors.push(`columns end at ${end.toFixed(1)}, area ends at ${(a.x + a.w).toFixed(1)}`);
  }

  const span = (from, to) => {
    const x = cols[from].x;
    return { x, w: cols[to].x + cols[to].w - x };
  };

  stageStrip(sheet, cols[0].x, a.y, colW, stageH, 'tb-det', 'DETECT', '0.1 day');
  stageStrip(sheet, cols[1].x, a.y, colW, stageH, 'tb-tri', 'TRIAGE', '0.2 day');
  const gather = span(2, 4);
  stageStrip(sheet, gather.x, a.y, gather.w, stageH, 'tb-gth', 'GATHER EVIDENCE', '0.4 day');
  stageStrip(sheet, cols[5].x, a.y, colW, stageH, 'tb-dia', 'DIAGNOSE', '0.5 day');
  stageStrip(sheet, cols[6].x, a.y, colW, stageH, 'tb-dec', 'DECIDE / APPROVE', '2.0 days');
  stageStrip(sheet, cols[7].x, a.y, colW, stageH, 'tb-capa', 'CAPA', '40.3 days');

  const laneMeta = [
    ['L-op', 'Operator', 'human', ''],
    ['L-tech', 'Technician', 'review', 'No hand-off'],
    ['L-pe', 'Process engineer', 'deploy', 'Reads the rank'],
    ['L-qe', 'Quality engineer', 'gov', ''],
    ['L-sc', 'Supply chain', 'gw', 'Lot is joined'],
    ['L-mgmt', 'Management', 'ok', ''],
    ['L-ag', 'MPS agent platform', 'ai', '(StatusNeo)'],
  ];
  let yCursor = lanesTop;
  const lanes = laneMeta.map((meta, i) => {
    const band = sheet.band(meta[0], a.x, yCursor, a.w, heights[i], meta[1], meta[2], meta[3]);
    yCursor += heights[i] + laneGap;
    return band;
  });

  const cardH = 118;
  const plainH = 88;
  const inset = 4;
  const stackGap = 20;

  const put = (id, col, lane, lines, tone, badges, slot, h = cardH) => {
    const c = cols[col];
    const row = lanes[lane];
    let y;
    if (slot === 'top' || slot === 'bottom') {
      const block = cardH * 2 + stackGap;
      const top0 = row.y + (row.h - block) / 2;
      y = slot === 'top' ? top0 : top0 + cardH + stackGap;
    } else {
      y = row.y + (row.h - h) / 2;
    }
    const opts = badges && badges.length ? { badges } : {};
    sheet.node(id, c.x + inset, y, c.w - inset * 2, h, lines, tone, opts);
  };

  put('op', 0, 0, ['Alert on the console', 'Northwater, MX-400 etch', 'Harness bar stays outside'], 'human', null, 'mid', plainH);
  put(
    'a1',
    0,
    6,
    ['SPC opens the ticket', 'Tool, chamber, and recipe', 'Nothing is spoken'],
    'ai',
    [agentBadge(1), removed(1)],
    'mid',
  );
  put(
    'a2',
    1,
    6,
    ['Ticket is the alert', 'Tool, time, and symptom', 'Nobody retypes it'],
    'ai',
    [agentBadge(2), removed(2)],
    'mid',
  );
  put(
    'h3',
    1,
    3,
    ['Opens the ticket', 'Recipe step stays on it', 'Takes the investigation'],
    'gov',
    [humanBadge(3), removed(3)],
    'mid',
  );
  put(
    'a4',
    2,
    6,
    ['Reads the MES', 'Runs, lots, and recipe', 'Not a spreadsheet paste'],
    'ai',
    [agentBadge(4), removed(4)],
    'top',
  );
  put(
    'a5',
    2,
    6,
    ['Reads historian, SPC, ECN', 'Change log is in the pack', 'Not checked by eye'],
    'ai',
    [agentBadge(5), removed(5)],
    'bottom',
  );
  put(
    'a6',
    3,
    6,
    ['Retrieves prior cases', 'Onto the same ticket', 'Not from a folder'],
    'ai',
    [agentBadge(6), removed(6)],
    'top',
  );
  put(
    'a7',
    3,
    6,
    ['Joins the supplier lot', 'To the manufacturing run', 'Not an email lookup'],
    'ai',
    [agentBadge(7), removed(7)],
    'bottom',
  );
  put(
    'a8',
    4,
    6,
    ['One evidence pack', 'Each file is cited once', 'No meeting to reconcile'],
    'ai',
    [agentBadge(8), removed(8)],
    'mid',
  );
  put(
    'a9',
    5,
    6,
    ['Ranks four cause classes', 'Cites what rules one out', 'Not the last known cause'],
    'ai',
    [agentBadge(9), removed(9)],
    'mid',
  );
  put(
    'a10',
    6,
    6,
    ['Pack is the ask', 'Citations are attached', 'No mail thread'],
    'ai',
    [agentBadge(10)],
    'mid',
  );
  put(
    'h11',
    6,
    5,
    ['Approves on the pack', 'Signed token, single use', 'The cause is cited'],
    'ok',
    [humanBadge(11), removed(10)],
    'mid',
  );
  put(
    'a12',
    7,
    6,
    ['Writes the CAPA', 'From the approved pack', 'Not re-keyed into the QMS'],
    'ai',
    [agentBadge(12), removed(11)],
    'top',
  );
  put(
    'a13',
    7,
    6,
    ['Token is the signature', 'Already given at approval', 'No second signature'],
    'ai',
    [agentBadge(13)],
    'bottom',
  );

  const down = (fromId, toId, id) => sheet.link(fromId, toId, 'bottom', 'top', { id, color: FLOW });
  const up = (fromId, toId, id) => sheet.link(fromId, toId, 'top', 'bottom', { id, color: FLOW });
  const right = (fromId, toId, id) => sheet.link(fromId, toId, 'right', 'left', { id, color: FLOW });

  down('op', 'a1', 'op-a1');
  right('a1', 'a2', 'a1-a2');
  up('a2', 'h3', 'a2-h3');
  handoff(sheet, 'h3', 'a4', 'h3-a4', { color: FLOW });
  down('a4', 'a5', 'a4-a5');
  handoff(sheet, 'a5', 'a6', 'a5-a6', { color: FLOW });
  down('a6', 'a7', 'a6-a7');
  handoff(sheet, 'a7', 'a8', 'a7-a8', { color: FLOW });
  right('a8', 'a9', 'a8-a9');
  right('a9', 'a10', 'a9-a10');
  up('a10', 'h11', 'a10-h11');
  handoff(sheet, 'h11', 'a12', 'h11-a12', { color: FLOW });
  down('a12', 'a13', 'a12-a13');

  const glossW = 700;
  const glossX = a.x + a.w - glossW;
  drawToBeCallouts(sheet, a.x, panelY, glossX - a.x - 16, panelH);
  drawToBeGlossary(sheet, glossX, panelY, glossW, panelH);
}

function drawToBeCallouts(sheet, x, y, w, h) {
  sheet.frame('removed-box', x, y, w, h, 'removed');
  const clip = { x, y, w, h };
  const left = x + 24;
  const mid = x + 24 + (w - 48) / 2;
  sheet.text('rm-title', 'ERROR REMOVED callouts', left, y + 26, {
    size: 16,
    weight: 'bold',
    fill: NAVY,
    clip,
  });
  sheet.text(
    'rm-dek1',
    '11 agent steps replace a hand-off. 2 human steps remain: open, then approve. Illustrative / simulated.',
    left,
    y + 50,
    { size: 14, weight: 'regular', fill: INK, clip },
  );
  sheet.text(
    'rm-dek2',
    'Detect through Diagnose is 1.2 days to a hypothesis, simulated. CAPA at 40.3 days is the record cycle, simulated.',
    left,
    y + 74,
    { size: 14, weight: 'regular', fill: INK, clip },
  );
  const leftLines = [
    'E1   Spoken alarm. Statistical process control opens the ticket with tool, chamber, and recipe.',
    'E2   Ticket retyped. The alert is the record. Nobody retypes it.',
    'E3   Data gap. The recipe step stays on the ticket. A reply cannot drop it.',
    'E4   Spreadsheet paste. The run sheet is read, not pasted.',
    'E5   Missed engineering change. The change log is read into the pack.',
    'E6   Data gap. Prior cases are retrieved onto the ticket.',
  ];
  const rightLines = [
    'E7   Wrong lot. The supplier lot is joined to the run lot.',
    'E8   Data gap. One evidence pack records which file was current.',
    'E9   Last-cause bias. Four cause classes are checked every time.',
    'E10  Uncited approval. The signed token cites the evidence pack.',
    'E11  Re-keyed record. The approved pack is written. It is not typed again.',
  ];
  leftLines.forEach((text, i) => {
    sheet.text(`rm-l${i}`, text, left, y + 108 + i * 24, { size: 14, weight: 'regular', fill: INK, clip });
  });
  rightLines.forEach((text, i) => {
    sheet.text(`rm-r${i}`, text, mid, y + 108 + i * 24, { size: 14, weight: 'regular', fill: INK, clip });
  });
}

function drawToBeGlossary(sheet, x, y, w, h) {
  sheet.frame('glossary', x, y, w, h, 'neutral');
  const clip = { x, y, w, h };
  const left = x + 24;
  sheet.text('tg-title', 'Glossary', left, y + 26, { size: 16, weight: 'bold', fill: NAVY, clip });
  sheet.text('tg-dek', 'Short forms on this sheet, spelled out.', left, y + 50, {
    size: 14,
    weight: 'regular',
    fill: INK,
    clip,
  });
  const terms = [
    'MES    manufacturing execution system',
    'SPC    statistical process control',
    'ECN    engineering change notice',
    'QMS    quality management system',
    'CAPA   corrective and preventive action',
    'SC-5 spans Northwater and Ironwood. Escalation does not approve.',
  ];
  terms.forEach((text, i) => {
    sheet.text(`tg-t${i}`, text, left, y + 86 + i * 22, { size: 14, weight: 'regular', fill: INK, clip });
  });
}

function stageRail(sheet, x, y, w, h, id, lines) {
  const gap = 10;
  const heights = lines.map((line) => sheet.em(sheet.fontOf(line.weight), line.size));
  const total = heights.reduce((sum, value) => sum + value, 0) + gap * (lines.length - 1);
  let cursor = y + (h - total) / 2;
  const cx = x + w / 2;
  const clip = { x, y, w, h };
  lines.forEach((line, i) => {
    const baseline = cursor + sheet.asc(sheet.fontOf(line.weight), line.size);
    sheet.text(`${id}-r${i}`, line.text, cx, baseline, {
      size: line.size,
      weight: line.weight,
      fill: line.fill,
      align: 'center',
      clip,
    });
    cursor += heights[i] + gap;
  });
}

function compareCard(sheet, id, x, y, w, h, lines, tone, badges) {
  const opts = badges && badges.length ? { badges } : {};
  sheet.node(id, x, y, w, h, lines, tone, opts);
}

export function drawCompare(sheet) {
  const a = sheet.area;
  const glossH = 156;
  const glossY = a.y + a.h - glossH;
  const top = a.y + 48;
  const bottom = glossY - 14;
  const rowGap = 10;
  const rowCount = 6;
  const rowH = (bottom - top - rowGap * (rowCount - 1)) / rowCount;
  const railW = 268;
  const benW = 500;
  const gap = 14;
  const colW = (a.w - railW - benW - gap * 3) / 2;
  const railX = a.x;
  const asX = railX + railW + gap;
  const toX = asX + colW + gap;
  const benX = toX + colW + gap;
  const bandW = toX + colW - railX;

  const headY = a.y + 26;
  const headClip = { x: a.x, y: a.y, w: a.w, h: 42 };
  sheet.text('h-stage', 'STAGE', railX + railW / 2, headY, {
    size: 16,
    weight: 'bold',
    fill: NAVY,
    align: 'center',
    clip: { ...headClip, x: railX, w: railW },
  });
  sheet.text('h-as', 'AS-IS  ·  current investigation', asX + colW / 2, headY, {
    size: 16,
    weight: 'bold',
    fill: NAVY,
    align: 'center',
    clip: { ...headClip, x: asX, w: colW },
  });
  sheet.text('h-to', 'TO-BE  ·  assisted investigation', toX + colW / 2, headY, {
    size: 16,
    weight: 'bold',
    fill: NAVY,
    align: 'center',
    clip: { ...headClip, x: toX, w: colW },
  });
  sheet.text('h-ben', 'BENEFITS  ·  simulated', benX + benW / 2, headY, {
    size: 16,
    weight: 'bold',
    fill: NAVY,
    align: 'center',
    clip: { ...headClip, x: benX, w: benW },
  });

  const manualBadge = [{ label: 'MANUAL', tone: 'manual' }];
  const humanBadge = [{ label: 'HUMAN', tone: 'human' }];
  const agentBadge = [{ label: 'AGENT', tone: 'ai' }];
  const bothBadge = [
    { label: 'HUMAN', tone: 'human' },
    { label: 'AGENT', tone: 'ai' },
  ];
  const stages = [
    {
      id: 'det',
      title: 'DETECT',
      asDur: '0.5 day as-is',
      toDur: '0.1 day to-be',
      asLines: ['Alarm noticed on the MX-400 etch.', 'The operator tells a technician in person.', '1 manual hand-off, simulated.'],
      asTone: 'review',
      asBadges: manualBadge,
      toLines: ['Statistical process control opens the ticket.', 'The console shows the alert. Nobody retypes it.', '0 manual hand-offs, simulated.'],
      toTone: 'det',
      toBadges: [],
    },
    {
      id: 'tri',
      title: 'TRIAGE',
      asDur: '0.5 day as-is',
      toDur: '0.2 day to-be',
      asLines: ['The technician types the ticket into email.', 'The quality engineer assigns an owner by reply.', '2 manual hand-offs, simulated.'],
      asTone: 'review',
      asBadges: manualBadge,
      toLines: ['The engineer opens the ticket.', 'The agent plans the pack and does not diagnose.', '1 human hand-off, simulated.'],
      toTone: 'ai',
      toBadges: bothBadge,
    },
    {
      id: 'gth',
      title: 'GATHER EVIDENCE',
      asDur: '2.0 days as-is',
      toDur: '0.4 day to-be',
      asLines: ['Run sheet, historian, control chart, change log.', 'Prior cases and supplier lots. A meeting compares them.', '5 manual hand-offs, simulated.'],
      asTone: 'review',
      asBadges: manualBadge,
      toLines: ['Read-only calls. One evidence pack.', 'One plain sentence on each evidence item.', '0 manual hand-offs, simulated.'],
      toTone: 'ai',
      toBadges: agentBadge,
    },
    {
      id: 'dia',
      title: 'DIAGNOSE',
      asDur: '1.0 day as-is',
      toDur: '0.5 day to-be',
      asLines: ['The process engineer states the last known cause.', 'The four cause classes are not checked as a set.', '1 manual hand-off, simulated.'],
      asTone: 'review',
      asBadges: manualBadge,
      toLines: ['Four causes are checked on every incident.', 'The agent ranks one and names what rules it out.', '0 manual hand-offs, simulated.'],
      toTone: 'ai',
      toBadges: agentBadge,
    },
    {
      id: 'dec',
      title: 'DECIDE / APPROVE',
      asDur: '2.0 days as-is',
      toDur: '2.0 days to-be',
      asLines: ['Approval is a meeting or a mail reply.', 'No evidence pack is attached to the ask.', '2 manual hand-offs, simulated.'],
      asTone: 'review',
      asBadges: manualBadge,
      toLines: ['Grounding rejects a claim with no citation.', 'The engineer approves with a signed token.', '1 human hand-off, simulated.'],
      toTone: 'human',
      toBadges: humanBadge,
    },
    {
      id: 'capa',
      title: 'CAPA',
      asDur: '62 days as-is',
      toDur: '40.3 days to-be',
      asLines: ['The record is re-keyed into the quality system.', 'Management signs it and does not see the files.', '2 manual hand-offs, simulated.'],
      asTone: 'review',
      asBadges: manualBadge,
      toLines: ['The action service writes the record.', 'The agent draft is not a write.', '0 manual hand-offs, simulated.'],
      toTone: 'det',
      toBadges: [],
    },
  ];

  stages.forEach((stage, i) => {
    const rowY = top + i * (rowH + rowGap);
    const bandH = rowH - 6;
    const bandY = rowY + 3;
    sheet.roundRect(railX, bandY, bandW, bandH, 8, {
      fill: '#E8EEF6',
      stroke: NAVY,
      'stroke-width': 1.15,
    });
    stageRail(sheet, railX + 8, bandY, railW - 16, bandH, `rail-${stage.id}`, [
      { text: stage.title, size: 16, weight: 'bold', fill: NAVY },
      { text: stage.asDur, size: 14, weight: 'semibold', fill: INK },
      { text: stage.toDur, size: 14, weight: 'regular', fill: INK },
      { text: 'simulated', size: 14, weight: 'semibold', fill: NAVY },
    ]);
    const cardH = bandH - 16;
    const cardY = bandY + 8;
    compareCard(sheet, `as-${stage.id}`, asX, cardY, colW, cardH, stage.asLines, stage.asTone, stage.asBadges);
    compareCard(sheet, `to-${stage.id}`, toX, cardY, colW, cardH, stage.toLines, stage.toTone, stage.toBadges);
  });

  const benefits = [
    ['Time to root cause', '4.0 days down to 1.2 days, simulated.', 'Detect through Diagnose only.', 'Approval days are not reduced.'],
    ['Fewer hand-offs', '13 manual hand-offs down to 2, simulated.', 'The engineer opens the ticket.', 'The engineer approves.'],
    ['Traceable evidence trail', 'One pack, with a citation on each claim.', 'The mail thread is not the record.', 'Simulated comparison.'],
    ['Consistent four-cause check', 'Engineering change, out of spec,', 'supply-chain material, and process drift.', 'The same four, every time.'],
    ['The human stays the approver', 'A signed token is required.', 'Agents do not write the record.', 'The person decides.'],
  ];
  const benGap = 12;
  const benH = (bottom - top - benGap * (benefits.length - 1)) / benefits.length;
  benefits.forEach((lines, i) => {
    const y = top + i * (benH + benGap);
    compareCard(sheet, `ben-${i}`, benX, y, benW, benH, lines, 'gw');
  });

  drawCompareGlossary(sheet, a.x, glossY, a.w, glossH);
}

function drawCompareGlossary(sheet, x, y, w, h) {
  const box = sheet.frame('cglossary', x, y, w, h, 'neutral');
  const clip = { x, y, w, h };
  const left = x + 24;
  sheet.text('cgl-title', 'Glossary', left, y + 28, { size: 16, weight: 'bold', fill: NAVY, clip });
  sheet.text('cgl-dek', 'Short forms on this sheet. All figures are simulated.', left, y + 54, {
    size: 14,
    weight: 'regular',
    fill: INK,
    clip,
  });
  sheet.text('cgl-capa', 'CAPA    corrective and preventive action', left, y + 84, {
    size: 14,
    weight: 'regular',
    fill: INK,
    clip,
  });
  sheet.text(
    'cgl-note1',
    'Detect through Diagnose is the time to a hypothesis: 4.0 days down to 1.2 days, simulated.',
    left,
    y + 112,
    { size: 14, weight: 'regular', fill: INK, clip },
  );
  sheet.text(
    'cgl-note2',
    'The CAPA row is the record cycle, simulated. Decide / approve stays at 2.0 days, simulated.',
    left,
    y + 136,
    { size: 14, weight: 'regular', fill: INK, clip },
  );
  return box;
}

export const SHEETS = [
  {
    sheetId: 'G1',
    issue: '#46',
    kicker: 'LOGICAL ARCHITECTURE',
    title: 'Logical architecture',
    slug: 'logical',
    version: 'v1.1',
    draw: drawLogical,
    legend: [
      { tone: 'det', label: 'Deterministic rules. Code, unit tested.' },
      { tone: 'ai', label: 'Probabilistic agents. No write tools.' },
      { tone: 'human', label: 'Human authority. Escalation blocks a bare approve.' },
      { tone: 'gov', label: 'Read-only evidence, sites, and scenario records.' },
      { tone: 'gw', label: 'Model gateway and the spend cap.' },
      { tone: 'neutral', label: 'Arrow: a handoff or a read.' },
    ],
  },
  {
    sheetId: 'G2',
    issue: '#47',
    kicker: 'PHYSICAL ARCHITECTURE',
    title: 'Physical architecture',
    slug: 'physical',
    version: 'v1.1',
    draw: drawPhysical,
    legend: [
      { tone: 'human', label: 'Identity. Entra, invite only.' },
      { tone: 'deploy', label: 'Resource group, harness, and console.' },
      { tone: 'gw', label: 'Targets, scale, and the spend cap.' },
      { tone: 'gov', label: 'Traces, Key Vault, and the simulated plants.' },
      { tone: 'review', label: 'Manual deploy gate.' },
      { tone: 'ok', label: 'OIDC. No stored cloud secrets.' },
    ],
  },
  {
    sheetId: 'G3',
    issue: '#48',
    kicker: 'CI/CD AND PROMOTION',
    title: 'CI/CD and promotion',
    slug: 'cicd',
    draw: drawCicd,
    legend: [
      { tone: 'lane', label: 'Grok Build lane, one PR.' },
      { tone: 'ok', label: 'Required on every pull request.' },
      { tone: 'later', label: 'Dashed. After integration (I1).' },
      { tone: 'review', label: 'Human review gate.' },
      { tone: 'deploy', label: 'Manual deploy.yml.' },
      { tone: 'human', label: 'Smoke after deploy.' },
    ],
  },
  {
    sheetId: 'G4',
    issue: '#56',
    kicker: 'INVESTIGATION FLOW',
    title: 'Investigation flow',
    slug: 'investigation-flow',
    version: 'v1.2',
    draw: drawInvestigation,
    legend: [
      { tone: 'ai', label: 'Agent step. Violet badge. No write.' },
      { tone: 'det', label: 'Deterministic. Dashed arrow escalates.' },
      { tone: 'human', label: 'Engineer. A bare approve stays off.' },
      { tone: 'neutral', label: 'Console. The harness bar is outside.' },
      { tone: 'gov', label: 'Read-only tools, including scenario text.' },
      { tone: 'gw', label: 'Model gateway. Completions only.' },
    ],
  },
  {
    sheetId: 'G5',
    issue: '#74',
    kicker: 'AS-IS INVESTIGATION',
    title: 'As-is investigation flow',
    slug: 'as-is-flow',
    draw: drawAsIs,
    legend: [
      { tone: 'human', label: 'Operator. Notices the alarm on the tool.' },
      { tone: 'review', label: 'Technician. The call, the ticket, the run sheet.' },
      { tone: 'deploy', label: 'Process engineer. Files, the meeting, the hypothesis.' },
      { tone: 'gov', label: 'Quality engineer. Owner, the thread, the record.' },
      { tone: 'gw', label: 'Supply chain. Supplier lots by email.' },
      { tone: 'ok', label: 'Management. Approves and signs.' },
      { tone: 'manual', label: 'Red MANUAL badge and red arrow. A hand-off.' },
      { tone: 'risk', label: 'Amber ERROR RISK badge. A fact can change.' },
    ],
  },
  {
    sheetId: 'G6',
    issue: '',
    kicker: 'TO-BE INVESTIGATION',
    title: 'To-be investigation flow',
    slug: 'to-be-flow',
    version: 'v1.1',
    draw: drawToBe,
    legend: [
      { tone: 'human', label: 'Operator. Sees the alert on the console.' },
      { tone: 'review', label: 'Technician. No hand-off on this path.' },
      { tone: 'deploy', label: 'Process engineer. Reads the rank. Does not state it.' },
      { tone: 'gov', label: 'Quality engineer. Opens the ticket. HUMAN 3.' },
      { tone: 'gw', label: 'Supply chain. No email lookup on this path.' },
      { tone: 'ok', label: 'Management. Approves. The human stays the approver.' },
      { tone: 'ai', label: 'MPS agent platform (StatusNeo). Violet AGENT badge and arrow.' },
      { tone: 'removed', label: 'Green ERROR REMOVED badge. E1 through E11, in the callout.' },
    ],
  },
  {
    sheetId: 'G5',
    issue: '#74',
    kicker: 'CURRENT VERSUS FUTURE',
    title: 'Current and future state',
    slug: 'as-is-vs-to-be',
    draw: drawCompare,
    legend: [
      { tone: 'review', label: 'As-is step. People, email, and files.' },
      { tone: 'manual', label: 'Red MANUAL badge. A hand-off.' },
      { tone: 'det', label: 'To-be rules. Code opens the ticket and writes.' },
      { tone: 'ai', label: 'To-be agent. Plans, gathers, and ranks.' },
      { tone: 'human', label: 'To-be human. Opens the ticket and approves.' },
      { tone: 'gw', label: 'Benefit. Every figure is simulated.' },
    ],
  },
  {
    sheetId: 'G7',
    issue: 'G7',
    kicker: 'FAB FLOOR PLAN',
    title: 'Physical fab floor',
    slug: 'fab-floor',
    strictLabels: true,
    draw: drawFabFloor,
    legend: [
      { tone: 'human', label: 'Cleanroom bay. Filtered air.' },
      { tone: 'gov', label: 'Utility chase. Sub-fab is below.' },
      { tone: 'gw', label: 'Overhead rail and stocker.' },
      { tone: 'det', label: 'Deposition tool. MPS-D7.' },
      { tone: 'review', label: 'Etch tool. MPS-E7.' },
      { tone: 'ai', label: 'Strip tool. MPS-S7.' },
      { tone: 'ok', label: 'Clean tool. MPS-C7.' },
      { tone: 'neutral', label: 'Plan view. Ports face the rail.' },
    ],
  },
];
