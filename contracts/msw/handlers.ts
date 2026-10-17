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
  siteId: "northwater",
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
  siteId: "northwater",
};

function requestedSite(request: Request): string | null {
  const value = new URL(request.url).searchParams.get("siteId");
  if (!value || value.trim().length === 0) return null;
  return value.trim().toLowerCase();
}

/** The contract fixture tools sit at Northwater. Another plant returns an empty list. */
function allowsNorthwater(request: Request): boolean {
  const wanted = requestedSite(request);
  return wanted === null || wanted === "northwater";
}

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
  http.get("/api/fleet/live", () => HttpResponse.json({ simulatedNow: "2026-09-01T00:02:00Z", tools: [] })),
  http.get("/api/tools/:toolId/live", () =>
    HttpResponse.json({
      toolId: "E-02",
      siteId: "northwater",
      simulatedNow: "2026-09-01T00:02:00Z",
      status: "nominal",
      readings: [],
      series: [],
    }),
  ),
  http.get("/api/alerts", ({ request }) =>
    !allowsNorthwater(request)
      ? HttpResponse.json([])
      : HttpResponse.json([
      {
        id: "33333333-3333-4333-8333-333333333333",
        toolId: "E-02",
        chamberId: "C1",
        parameter: "escTemperature",
        severity: "high",
        rule: "westernElectric1",
        raisedAt: "2026-10-14T14:59:00Z",
        correlationId: "corr-s1",
        siteId: "northwater",
      },
    ]),
  ),
  http.get("/api/tickets", ({ request }) =>
    !allowsNorthwater(request)
      ? HttpResponse.json([])
      : HttpResponse.json([
      {
        id: "44444444-4444-4444-8444-444444444444",
        number: "TKT-1001",
        incidentId: incident.id,
        toolId: "E-02",
        title: incident.summary,
        openedAt: incident.openedAt,
        siteId: "northwater",
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
  http.get("/api/investigations", ({ request }) => HttpResponse.json(allowsNorthwater(request) ? [investigation] : [])),
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
  http.post("/api/printouts/inspect", () =>
    HttpResponse.json({
      toolId: "E-01",
      chamberId: "E-01-A",
      observedAt: "2026-10-12T14:22:00Z",
      instrument: "oscilloscope",
      parameter: "rfPower",
      value: 2140,
      unit: "W",
      specMinimum: 1000,
      specMaximum: 2000,
      shape: "oscillation",
      operatorInitials: "KL",
      parseMethod: "text",
      outOfSpec: true,
    }),
  ),
  http.get("/api/manual-readings", () => HttpResponse.json([])),
  http.get("/api/manual-readings/:evidenceId/file", () => new HttpResponse(null, { status: 404 })),
  http.post("/api/printouts/confirm", () =>
    HttpResponse.json({
      evidenceId: "77777777-7777-4777-8777-777777777777",
      incidentId: "66666666-6666-4666-8666-666666666666",
      faultId: "55555555-5555-4555-8555-555555555555",
      correlationId: "corr-printout",
      alertId: "44444444-4444-4444-8444-444444444444",
      appliedToSimulation: true,
      outOfSpec: true,
      printedParameter: "rfPower",
      catalogParameter: "rfSourcePower",
    }),
  ),
  http.get("/api/audit", () => HttpResponse.json({ entries: [], traces: [] })),
  http.get("/api/admin/session", () =>
    HttpResponse.json({ mode: "local", isAdmin: true, displayName: "dev-user" }),
  ),
  http.get("/api/admin/reset/progress", () =>
    HttpResponse.json({ running: false, kind: null, phase: "idle", message: "Ready." }),
  ),
  http.post("/api/admin/reset", async ({ request }) => {
    let kind: "hard" | "soft" = "hard";
    try {
      const body = (await request.json()) as { kind?: string };
      if (body.kind === "soft") kind = "soft";
    } catch {
      kind = "hard";
    }
    return HttpResponse.json({
      seed: 4242,
      simulatedOrigin: "2026-09-01T00:00:00Z",
      fleetToolCount: 8,
      ticketCount: 0,
      investigationCount: 0,
      approvalCount: 0,
      capaCount: 0,
      telemetryCount: 0,
      eventCount: 1,
      resetEventId: "reset-event",
      kind,
    });
  }),
  http.get("/api/demo/data-mode", () => HttpResponse.json({ mode: "live", scenarioId: null, messageCount: 0 })),
  http.post("/api/demo/data-mode", async ({ request }) => {
    const body = (await request.json()) as { mode?: string; scenarioId?: string };
    if (body.mode === "live") return HttpResponse.json({ mode: "live", scenarioId: null, messageCount: 0 });
    if (body.mode === "replay" && body.scenarioId) {
      return HttpResponse.json({ mode: "replay", scenarioId: body.scenarioId, messageCount: 12 });
    }
    return HttpResponse.json({ title: "That scenario has no recording.", status: 404 }, { status: 404 });
  }),
  http.post("/api/admin/replay/:scenarioId", ({ params }) =>
    HttpResponse.json({ scenarioId: String(params.scenarioId), messageCount: 12 }),
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
        whatsHappening: "An edge ring is the ring around the wafer that shapes the process at the wafer edge. Etch tool E-02 received revision C of that ring.",
        whatWeSimulate: "The chuck temperature sensor on E-02 chamber A oscillates by about 3.5 standard deviations.",
        whatAiShouldFind: "The temperature swing lines up with the engineering change that installed edge ring revision C.",
        expectedCause: "Edge ring revision C on E-02.",
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
        whatsHappening: "The thermocouple on etch tool E-01 is overdue for calibration. The reading drifts away from the true temperature.",
        whatWeSimulate: "Chuck temperature on E-01 chamber A ramps about 4 standard deviations high over about 2 minutes of real time.",
        whatAiShouldFind: "The drift matches an overdue thermocouple calibration.",
        expectedCause: "Overdue thermocouple calibration on E-01.",
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
        whatsHappening: "Both etch tools use the same bottle of CF4. That bottle arrived out of specification.",
        whatWeSimulate: "CF4 flow on E-01 and E-02 steps down by about 12 percent.",
        whatAiShouldFind: "The shared gas lot and the supplier notice explain both tools.",
        expectedCause: "The shared gas lot is out of specification.",
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
        whatsHappening: "After a chamber clean, the etch chamber slowly seasons and the etch rate falls.",
        whatWeSimulate: "Etch rate on E-01 chamber A ramps down by about 3.5 standard deviations over about 2 minutes of real time.",
        whatAiShouldFind: "The slow ramp has no engineering change and no bad material lot.",
        expectedCause: "Chamber seasoning after a clean on E-01.",
      },
      {
        id: "SC-5",
        title: "Precursor lot on two plants",
        rootCauseCategory: "supplyChainMaterial",
        toolIds: ["D-03", "I-D-01"],
        chamberId: "D-03-A",
        parameter: "filmThickness",
        shape: "step",
        summary: "Precursor lot MX4419 is thin, so film thickness steps down on D-03 and I-D-01.",
        whatsHappening: "A precursor is the material that becomes the deposited film. Lot MX4419 went to D-03 and I-D-01.",
        whatWeSimulate: "Film thickness on both tools steps down by about 8 percent.",
        whatAiShouldFind: "Both plants share the precursor lot. The chamber clean on only one tool is a confounder.",
        expectedCause: "Precursor lot MX4419 is thin at both plants.",
      },
      {
        id: "SC-7",
        title: "Pump bearing wear",
        rootCauseCategory: "equipmentCalibrationOrAlignment",
        toolIds: ["D-02"],
        chamberId: "D-02-A",
        parameter: "vibrationRms",
        shape: "ramp",
        summary: "Foreline pump vibration on D-02 rises as the bearing runs past its service interval.",
        whatsHappening: "The foreline pump is the vacuum pump that pulls gas out of the chamber. Its bearing is past the service interval on D-02.",
        whatWeSimulate: "Pump vibration on D-02 ramps up by about 5 standard deviations over about two and a half minutes.",
        whatAiShouldFind: "The maintenance record says the bearing hours are past the service interval.",
        expectedCause: "Foreline pump bearing wear on D-02.",
      },
      {
        id: "SC-8",
        title: "Recipe setpoint edited",
        rootCauseCategory: "processDrift",
        toolIds: ["E-03"],
        chamberId: "E-03-A",
        parameter: "rfBiasPower",
        shape: "step",
        summary: "An operator raised the RF power setpoint on E-03 and did not file an engineering change.",
        whatsHappening: "RF power is the radio-frequency power that drives the etch. An operator edited that setpoint on E-03 with no engineering change order.",
        whatWeSimulate: "RF bias power on E-03 steps up by about 4.5 standard deviations. No other tool moves.",
        whatAiShouldFind: "The note says the operator changed the recipe setpoint and there is no engineering change order.",
        expectedCause: "Unauthorized RF power recipe edit on E-03.",
      },
      {
        id: "SC-6",
        title: "Oscilloscope RF oscillation on E-01",
        rootCauseCategory: "equipmentCalibrationOrAlignment",
        toolIds: ["E-01"],
        chamberId: "E-01-A",
        parameter: "rfSourcePower",
        shape: "oscillation",
        summary: "An oscilloscope printout shows source RF power oscillating out of spec on E-01 chamber A.",
      },
    ]),
  ),
  http.post("/api/injector/stop", () => HttpResponse.json({ removed: 0 })),
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
  http.get("/api/metadata/company", () =>
    HttpResponse.json({
      id: "meridian",
      name: "Meridian Precision Systems",
      purpose: "Make precision semiconductor wafers.",
      description: "Three fictional plants.",
      industry: "Semiconductor manufacturing",
      loadMilliseconds: 1,
    }),
  ),
  http.get("/api/metadata/sites", () =>
    HttpResponse.json([
      {
        id: "northwater",
        name: "Northwater Fab",
        purpose: "Run production wafers.",
        description: "The production plant.",
        timezone: "America/Denver",
        capabilities: [],
      },
    ]),
  ),
  http.get("/api/metadata/tools/:toolId", () =>
    HttpResponse.json({
      id: "E-01",
      name: "Etch tool in bay one",
      toolType: "Conductor etch",
      siteId: "northwater",
      siteName: "Northwater Fab",
      lineId: "nw-etch",
      lineName: "Northwater wafer etch",
      capabilityId: null,
      simulated: true,
      inputs: [{ name: "Patterned wafer", description: "A wafer." }],
      outputs: [{ name: "Etched wafer", description: "An opened film." }],
      parameters: [],
    }),
  ),
  http.get("/api/metadata/rules", () => HttpResponse.json([])),
  http.get("/api/metadata/settings", () => HttpResponse.json([])),
  http.get("/api/trace/:eventId", () =>
    HttpResponse.json({
      focusEventId: "01ARZ3NDEKTSV4RRFFQ69G5FAV",
      rootEventId: "01TRACE00000000000000000001",
      ancestors: [
        {
          eventId: "01TRACE00000000000000000001",
          type: "command.scenario.start",
          summary: "Scenario Edge ring revision C on Etch 02 started.",
          occurredAt: "2026-09-01T00:00:00Z",
          toolId: "E-02",
          toolName: "Etch 02",
          value: null,
          limitLow: null,
          limitHigh: null,
          children: [],
          truncated: false,
        },
        {
          eventId: "01TRACE00000000000000000002",
          type: "command.fault.inject",
          summary: "A presenter injected a fault on Etch 02.",
          occurredAt: "2026-09-01T00:00:01Z",
          toolId: "E-02",
          toolName: "Etch 02",
          value: null,
          limitLow: null,
          limitHigh: null,
          children: [],
          truncated: false,
        },
      ],
      tree: {
        eventId: "01ARZ3NDEKTSV4RRFFQ69G5FAV",
        type: "telemetry.reading",
        summary: "Chuck temperature was 53.1.",
        occurredAt: "2026-09-01T00:02:00Z",
        toolId: "E-02",
        toolName: "Etch 02",
        value: 53.1,
        limitLow: 40,
        limitHigh: 60,
        truncated: false,
        children: [
          {
            eventId: "01TRACE00000000000000000003",
            type: "alert.raised",
            summary: "A process chart alert fired for chuck temperature.",
            occurredAt: "2026-09-01T00:03:00Z",
            toolId: "E-02",
            toolName: "Etch 02",
            value: 53.1,
            limitLow: 40,
            limitHigh: 60,
            truncated: false,
            children: [
              {
                eventId: "01TRACE00000000000000000004",
                type: "rule.matched",
                summary: "The business rule Edge ring temperature fired.",
                occurredAt: "2026-09-01T00:04:00Z",
                toolId: "E-02",
                toolName: "Etch 02",
                value: null,
                limitLow: null,
                limitHigh: null,
                truncated: false,
                children: [
                  {
                    eventId: "01TRACE00000000000000000005",
                    type: "ticket.created",
                    summary: "A ticket was opened for process engineering.",
                    occurredAt: "2026-09-01T00:05:00Z",
                    toolId: "E-02",
                    toolName: "Etch 02",
                    value: null,
                    limitLow: null,
                    limitHigh: null,
                    truncated: false,
                    children: [
                      {
                        eventId: "01TRACE00000000000000000006",
                        type: "command.investigation.start",
                        summary: "An investigation was started for Etch 02.",
                        occurredAt: "2026-09-01T00:06:00Z",
                        toolId: "E-02",
                        toolName: "Etch 02",
                        value: null,
                        limitLow: null,
                        limitHigh: null,
                        truncated: false,
                        children: [
                          {
                            eventId: "01TRACE00000000000000000007",
                            type: "investigation.step",
                            summary: "The evidence was added to the investigation.",
                            occurredAt: "2026-09-01T00:07:00Z",
                            toolId: "E-02",
                            toolName: "Etch 02",
                            value: null,
                            limitLow: null,
                            limitHigh: null,
                            children: [],
                            truncated: false,
                          },
                        ],
                      },
                    ],
                  },
                ],
              },
            ],
          },
        ],
      },
    }),
  ),
];
