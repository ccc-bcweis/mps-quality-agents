// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company
// MSW handlers derived from contracts/openapi.yaml for the UI lane.
// This branch has no web app yet. Import these once the `msw` package is installed.

import { http, HttpResponse } from "msw";

const tool = {
  id: "E-02",
  name: "Etch 02",
  family: "etch",
  model: "MPS-E7",
  bay: "Bay-2",
  siteId: "northwater",
  chambers: [{ id: "C1", name: "Chamber 1" }],
};

const throughput = {
  waferStartsPerHour: 18.4,
  waferOutsPerHour: 17.6,
  wip: 42,
  yieldPct: 96.8,
  uptimePct: 95.1,
};

const incident = {
  id: "8d0a1c4e-1b7a-4e5a-9c1d-0a6b2e3f4a5b",
  toolId: "E-02",
  chamberId: "C1",
  parameter: "escTemperature",
  channel: "iotSpcAlert",
  severity: "high",
  summary: "ESC temperature oscillation after a recipe revision.",
  correlationId: "corr-s1",
  openedAt: "2026-10-14T15:00:00Z",
  stage: "investigating",
};

const investigation = {
  id: "2c6e1a40-7b2d-4f1e-8a33-91d0c5b6e7f8",
  incidentId: incident.id,
  stage: "recommendationReady",
  correlationId: incident.correlationId,
  evidence: [
    {
      id: "11111111-1111-4111-8111-111111111111",
      source: "plm",
      title: "ECO-4471 edge ring rev C",
      excerpt: "Edge ring revision C became effective before the oscillation.",
      observedAt: "2026-10-12T08:00:00Z",
      toolId: "E-02",
      externalRef: "ECO-4471",
    },
  ],
  hypotheses: [
    {
      id: "22222222-2222-4222-8222-222222222222",
      category: "engineeringChange",
      cause: "Edge ring revision C changed the thermal path.",
      confidence: 0.74,
      evidenceIds: ["11111111-1111-4111-8111-111111111111"],
      counterEvidence: ["A preventive maintenance was performed the same week."],
      actions: ["Hold the affected lots and confirm the installed ring revision."],
    },
  ],
  escalationReason: null,
};

export const handlers = [
  http.get("/health", () => HttpResponse.json({ status: "ok", service: "mps-api" })),
  http.get("/api/sites", () =>
    HttpResponse.json([
      {
        id: "northwater",
        name: "Northwater Fab",
        role: "Front-end wafer fab",
        location: "Cedar Reach, Valecrest",
        timezone: "America/Denver",
        bays: [{ id: "Bay-1", name: "Bay 1", toolCount: 4 }],
        kpis: throughput,
      },
    ]),
  ),
  http.get("/api/sites/compare", () =>
    HttpResponse.json({
      family: "etch",
      referenceSiteId: "northwater",
      parameters: [
        {
          parameter: "escTemperature",
          unit: "C",
          sites: [
            {
              siteId: "northwater",
              siteName: "Northwater Fab",
              mean: 50.1,
              sigma: 1.2,
              alarmRate: 0.01,
              uptimePct: 96.2,
              yieldPct: 97.4,
              discrepancy: false,
              reason: null,
            },
            {
              siteId: "ironwood",
              siteName: "Ironwood Fab",
              mean: 62.0,
              sigma: 2.6,
              alarmRate: 0.22,
              uptimePct: 92.1,
              yieldPct: 94.8,
              discrepancy: true,
              reason:
                "Ironwood Fab's electrostatic chuck temperature averages about 12 °C higher than the same tool family at Northwater Fab, and the readings wander more.",
            },
          ],
        },
      ],
    }),
  ),
  http.get("/api/sites/:siteId", () =>
    HttpResponse.json({
      id: "northwater",
      name: "Northwater Fab",
      role: "Front-end wafer fab",
      location: "Cedar Reach, Valecrest",
      timezone: "America/Denver",
      bays: [
        {
          id: "Bay-1",
          name: "Bay 1",
          tools: [
            {
              id: "E-01",
              name: "Etch E-01",
              family: "etch",
              model: "MPS-E7",
              status: "nominal",
              x: 22,
              y: 30,
              siteId: "northwater",
            },
          ],
          kpis: throughput,
        },
      ],
      flows: [{ fromBayId: "Bay-3", toBayId: "Bay-1", label: "Deposited wafers move to bay 1 for etch." }],
      kpis: throughput,
    }),
  ),
  http.get("/api/tools", () => HttpResponse.json([tool])),
  http.get("/api/tools/:toolId", () => HttpResponse.json(tool)),
  http.get("/api/alerts", () =>
    HttpResponse.json([
      {
        id: "33333333-3333-4333-8333-333333333333",
        toolId: "E-02",
        chamberId: "C1",
        parameter: "escTemperature",
        severity: "high",
        rule: "westernElectric1",
        raisedAt: "2026-10-14T14:59:00Z",
        correlationId: "corr-s1",
      },
    ]),
  ),
  http.get("/api/tickets", () =>
    HttpResponse.json([
      {
        id: "44444444-4444-4444-8444-444444444444",
        number: "TKT-1001",
        incidentId: incident.id,
        toolId: "E-02",
        title: incident.summary,
        openedAt: incident.openedAt,
      },
    ]),
  ),
  http.get("/api/incidents", () => HttpResponse.json([incident])),
  http.post("/api/incidents", async ({ request }) => {
    const body = (await request.json()) as { toolId?: string; parameter?: string; summary?: string };
    return HttpResponse.json(
      {
        ...incident,
        id: "55555555-5555-4555-8555-555555555555",
        toolId: body.toolId ?? "E-02",
        parameter: body.parameter ?? "escTemperature",
        summary: body.summary ?? "Operator report.",
        channel: "operatorReport",
        stage: "ticketOpened",
      },
      { status: 201 },
    );
  }),
  http.get("/api/incidents/:incidentId", () => HttpResponse.json(incident)),
  http.get("/api/incidents/:incidentId/investigation", () => HttpResponse.json(investigation)),
  http.post("/api/incidents/:incidentId/investigations", () =>
    HttpResponse.json({ ...investigation, stage: "started", hypotheses: [] }, { status: 201 }),
  ),
  http.get("/api/investigations/:investigationId", () => HttpResponse.json(investigation)),
  http.post("/api/investigations/:investigationId/decisions", async ({ request }) => {
    const body = (await request.json()) as { kind?: string; approverId?: string; comment?: string | null };
    if (body.kind === "override" && (body.comment ?? "").trim().length < 20) {
      return HttpResponse.json(
        { title: "Override needs a written justification of at least 20 characters.", status: 400 },
        { status: 400, headers: { "Content-Type": "application/problem+json" } },
      );
    }
    if ((body.kind === "approve" || body.kind === "override") && !body.approverId) {
      return HttpResponse.json(
        { title: "Approval requires an approver.", status: 400 },
        { status: 400, headers: { "Content-Type": "application/problem+json" } },
      );
    }
    const capa =
      body.kind === "approve" || body.kind === "override"
        ? {
            id: "66666666-6666-4666-8666-666666666666",
            incidentId: incident.id,
            summary: "Hold the affected lots and confirm the edge ring revision.",
            approvalTokenId: "77777777-7777-4777-8777-777777777777",
            actionHash: "ABC123",
            stage: "open",
            openedAt: "2026-10-14T15:05:00Z",
            approvedBy: body.approverId ?? "dev-user",
            approvedAt: "2026-10-15T02:42:00.000Z",
          }
        : null;
    return HttpResponse.json({
      investigation: {
        ...investigation,
        stage: body.kind === "approve" || body.kind === "override" ? "approved" : body.kind === "reject" ? "rejected" : "escalated",
      },
      capa,
    });
  }),
  http.get("/api/capas", () => HttpResponse.json([])),
  http.get("/api/capas/:capaId", () =>
    HttpResponse.json(
      { title: "CAPA was not found.", status: 404 },
      { status: 404, headers: { "Content-Type": "application/problem+json" } },
    ),
  ),
  http.post("/api/capas/:capaId/advance", () =>
    HttpResponse.json(
      { title: "Illegal CAPA transition.", status: 409 },
      { status: 409, headers: { "Content-Type": "application/problem+json" } },
    ),
  ),
  http.post("/api/faults", () =>
    HttpResponse.json(
      { faultId: "88888888-8888-4888-8888-888888888888", correlationId: "corr-inject" },
      { status: 202 },
    ),
  ),
  http.get("/api/audit", () => HttpResponse.json({ entries: [], traces: [] })),
  http.get("/api/admin/session", () =>
    HttpResponse.json({ mode: "local", isAdmin: true, displayName: "dev-user" }),
  ),
  http.post("/api/admin/reset", () =>
    HttpResponse.json({
      seed: 4242,
      simulatedOrigin: "2026-09-01T00:00:00Z",
      fleetToolCount: 8,
      ticketCount: 0,
      investigationCount: 0,
      approvalCount: 0,
      capaCount: 0,
    }),
  ),
  http.get("/api/injector/scenarios", () =>
    HttpResponse.json([
      {
        id: "SC-1",
        title: "Edge ring revision C on E-02",
        rootCauseCategory: "engineeringChange",
        toolIds: ["E-02"],
        chamberId: "E-02-A",
        parameter: "escTemperature",
        shape: "oscillation",
        summary: "ESC temperature oscillates on E-02 chamber A after edge ring revision C.",
      },
      {
        id: "SC-2",
        title: "ESC thermocouple calibration drift",
        rootCauseCategory: "equipmentCalibrationOrAlignment",
        toolIds: ["E-01"],
        chamberId: "E-01-A",
        parameter: "escTemperature",
        shape: "ramp",
        summary: "The ESC thermocouple on E-01 chamber A drifts because its calibration is overdue.",
      },
      {
        id: "SC-3",
        title: "Bad precursor gas lot",
        rootCauseCategory: "supplyChainMaterial",
        toolIds: ["E-01", "E-02"],
        chamberId: "E-01-A",
        parameter: "cf4Flow",
        shape: "step",
        summary: "Precursor gas lot PL-2044 is out of spec and both etch tools step off on CF4 flow.",
      },
      {
        id: "SC-4",
        title: "Slow chamber seasoning drift",
        rootCauseCategory: "processDrift",
        toolIds: ["E-01"],
        chamberId: "E-01-A",
        parameter: "etchRate",
        shape: "ramp",
        summary: "Etch rate on E-01 chamber A ramps slowly as the chamber seasons after a clean.",
      },
    ]),
  ),
  http.post("/api/injector/scenarios/:scenarioId", () =>
    HttpResponse.json(
      {
        scenarioId: "SC-1",
        seed: 4242,
        faultId: "99999999-9999-4999-8999-999999999999",
        correlationId: "corr-scenario-SC-1",
        telemetry: [
          {
            toolId: "E-02",
            chamberId: "E-02-A",
            parameter: "escTemperature",
            at: "2026-09-01T00:00:00Z",
            value: 51.2,
          },
        ],
      },
      { status: 202 },
    ),
  ),
  http.get("/api/metrics", () =>
    HttpResponse.json({
      mode: "replay",
      completionRate: null,
      groundingScore: null,
      categoryAccuracy: null,
      toolFailureCount: 0,
      latencyP95Ms: null,
      costPerInvestigationUsd: null,
      spendTodayUsd: 0,
      spendCapUsd: 2,
    }),
  ),
];
