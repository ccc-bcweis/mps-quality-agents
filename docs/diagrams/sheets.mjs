// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company
// Hand-tuned ARCH D layouts. Mermaid auto-layout crosses these graphs.

import { INK, NAVY } from './sheet.mjs';

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
    ['Fault Injector', 'UI throws a shaped fault'],
    ['Fleet simulator', '1 Hz telemetry, seeded'],
    ['Operator report', 'POST /api/incidents'],
    ['QMS NCR', 'Nonconformance event'],
    ['Supplier notice', 'Material lot signal'],
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
    'Impact features',
    'Scope and timing, passed to the agent as features',
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
    ['Corpus', 'SOP, FMEA, bulletins, CoA, decoy, injection'],
    ['Chunk and embed', 'Heading-aware chunks, versioned index'],
    ['Hybrid retrieval', 'BM25 plus vector, family and revision filters'],
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
    ['Orchestrator', 'MAF behind IInvestigationWorkflow'],
    ['Investigation Agent', 'Read-only tools, at most 10 calls'],
    ['Evidence gate', 'Gaps escalate. No root cause asserted.'],
    ['Resolution Agent', 'No tools. Category, citations, confounders'],
    ['Grounding validator', 'Cited IDs must be in the pack'],
    ['React console', 'The engineer decides'],
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
    ['Azure OpenAI', 'gpt-4o-mini and gpt-4o'],
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
    ['Approval token', 'HMAC, single-use, bound'],
    ['Action service', 'CAPA write. Not an agent tool.'],
    ['Mock QMS', 'Creates the CAPA, or a forced 500'],
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
  sheet.node('swa', swaX, 395, swaW, 110, ['Static Web App', 'React console'], 'deploy');
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
  sheet.node('sim', c0, acaY + 40, cW, 86, ['Simulator', '1 replica, 1 Hz'], 'deploy');
  sheet.node('api', c0, rowY, cW, rowH, ['API', '.NET 8, SignalR, scale 0-3'], 'deploy');
  sheet.node('worker', c0 + (cW + g3), rowY, cW, rowH, ['Worker', 'Scales on queue depth'], 'deploy');
  sheet.node('mock', c0 + 2 * (cW + g3), rowY, cW, rowH, ['Mock enterprise', 'QMS, ERP, MES, PLM, CMMS'], 'deploy');
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
    ['aoai', 'Azure OpenAI', 'gpt-4o-mini and gpt-4o, TPM quota'],
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
    ['L-console', 'Console screen', 'neutral', 'Glossary plain language'],
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
  put('alerts', 0, 1, plain, ['Alerts, then Tickets', 'Both screens update', 'Lifecycle bar moves'], 'neutral');
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
  put('reads', 3, 5, plain, ['Enterprise reads', 'MES, QMS, PLM, ERP', 'CMMS and documents'], 'gov');

  put('why', 4, 3, agent, ['Why it matters', 'One plain line per item', 'Written on the evidence'], 'ai', { badge: 'AGENT' });
  put('gate', 4, 2, plain, ['Evidence gate', 'Sufficient, continue', 'Else stop and list gaps'], 'det');
  put('esc-ui', 4, 1, plain, ['Request info', 'Missing or contradictory', 'Named gaps on screen'], 'neutral');
  put('esc-human', 4, 0, plain, ['No root cause', 'The engineer owns the gap', 'Nothing is asserted'], 'human');

  put('rank', 5, 4, agent, ['Rank causes', 'Writes the matrix below', 'Cites the confounder'], 'ai', { badge: 'AGENT' });

  put('gw-rank', 5, 6, plain, ['Model call', 'Ranking route', 'At most two calls'], 'gw');

  put('draft', 6, 4, agent, ['CAPA draft', 'Actions, not a write', 'Same agent output'], 'ai', { badge: 'AGENT' });
  put('ground', 6, 2, plain, ['Grounding check', 'Why-line cites a real item', 'Rejects a missing item'], 'det');

  put('appr-ui', 7, 1, plain, ['Approval screen', 'Recommendation shown', 'Explicit confirm'], 'neutral');
  put('appr', 7, 0, agent, ['Approval gate', 'Signed token', 'Single use, then it expires'], 'human', { badge: 'HUMAN' });

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
    ['QMS    quality management system', ''],
  ];
  const col2 = x + 28 + 320;
  terms.forEach((pair, i) => {
    const baseline = y + 82 + i * 28;
    sheet.text(`gl-a${i}`, pair[0], left, baseline, { size: 14, weight: 'regular', fill: INK, clip });
    if (pair[1]) sheet.text(`gl-b${i}`, pair[1], col2, baseline, { size: 14, weight: 'regular', fill: INK, clip });
  });
  return box;
}

export const SHEETS = [
  {
    sheetId: 'G1',
    issue: '#46',
    kicker: 'LOGICAL ARCHITECTURE',
    title: 'Logical architecture',
    slug: 'logical',
    draw: drawLogical,
    legend: [
      { tone: 'det', label: 'Deterministic rules. Code, unit tested.' },
      { tone: 'ai', label: 'Probabilistic agents. No write tools.' },
      { tone: 'human', label: 'Human authority. The only write path.' },
      { tone: 'gov', label: 'Read-only evidence, audit, eval.' },
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
    draw: drawPhysical,
    legend: [
      { tone: 'human', label: 'Identity. Entra, invite only.' },
      { tone: 'deploy', label: 'Resource group and Container Apps.' },
      { tone: 'gw', label: 'Targets, scale, and the spend cap.' },
      { tone: 'gov', label: 'Traces, Key Vault, ci.yml.' },
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
    version: 'v1.1',
    draw: drawInvestigation,
    legend: [
      { tone: 'ai', label: 'Agent step. Violet badge. No write.' },
      { tone: 'det', label: 'Deterministic. Dashed arrow escalates.' },
      { tone: 'human', label: 'Engineer. Only a human approves.' },
      { tone: 'neutral', label: 'Console. Glossary plain language.' },
      { tone: 'gov', label: 'Read-only tools, then the quality write.' },
      { tone: 'gw', label: 'Model gateway. Completions only.' },
    ],
  },
];
