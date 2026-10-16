// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company
// Browser mocks. List routes use the demo fleet. Health, metrics, and the
// CAPA advance route fall through to contracts/msw/handlers.ts.

import { http, HttpResponse } from "msw";
import { handlers as contractHandlers } from "../../../contracts/msw/handlers";
import type { Alert, BusinessRule, CapabilityType, FaultInjectionRequest, Incident, Investigation, MetadataLine, MetadataSite, MetadataToolCard, OpenIncidentRequest, PrintoutConfirmRequest, PrintoutInspection, ProcessMap, ScenarioRun, Ticket } from "../api/types";
import { telemetryForTool } from "../fleet/telemetry";
import { latestInvestigationForIncident } from "../investigation/ticketRoute";
import { incidents, investigationFor, investigations, tools } from "./demoData";
import { toolInPlant } from "../sites/selection";
import { matchesPlant } from "./plants";
import { familyComparison, siteDetail, siteSummaries, toolRecord } from "./sites/fixtures";
import { mockExtras, resetMockState } from "./state";

function requestedPlant(request: Request): string {
  return new URL(request.url).searchParams.get("siteId") ?? "all";
}

function problem(title: string, status: number) {
  return HttpResponse.json(
    { title, status },
    { status, headers: { "content-type": "application/problem+json" } },
  );
}

function clone<T>(value: T): T {
  return structuredClone(value);
}

function toolCard(id: string, name: string, simulated = true): MetadataToolCard {
  return { id, name, toolType: "Process tool", simulated };
}

function flowLine(id: string, name: string, stepNames: string[]): MetadataLine {
  return {
    id,
    name,
    lineType: "end-to-end",
    sequence: 0,
    characteristics: "A wafer visits strip, clean, thin-film deposition, and etch, in that order.",
    stepNames,
    tools: [],
  };
}

function workLine(id: string, name: string, lineType: string, sequence: number, characteristics: string, tools: MetadataToolCard[]): MetadataLine {
  return { id, name, lineType, sequence, characteristics, stepNames: [], tools };
}

function parked(id: string, name: string, type: CapabilityType, description: string) {
  return { id, name, type, description, simulated: false, lines: [], directTools: [] };
}

function metadataSites(): MetadataSite[] {
  return [
    {
      id: "northwater",
      name: "Northwater Fab",
      purpose: "Run production wafers.",
      description: "The production plant.",
      timezone: "America/Denver",
      capabilities: [
        {
          id: "nw-manufacturing",
          name: "Manufacturing",
          type: "manufacturing",
          description: "Wafer strip, clean, deposition, and etch at Northwater Fab.",
          simulated: true,
          lines: [
            flowLine("nw-end-to-end", "Northwater wafer flow", [
              "Northwater photoresist strip",
              "Northwater clean",
              "Northwater thin-film deposition",
              "Northwater wafer etch",
            ]),
            workLine("nw-strip", "Northwater photoresist strip", "photoresist-strip", 1, "Remote plasma removes photoresist before the rinse.", [
              toolCard("S-01", "Strip tool in bay one"),
            ]),
            workLine("nw-clean", "Northwater clean", "clean", 2, "A wet bench rinses the wafer after strip.", [toolCard("C-01", "Clean tool in bay one")]),
            workLine("nw-deposition", "Northwater thin-film deposition", "thin-film-deposition", 3, "A chemical vapor deposit grows the film.", [
              toolCard("D-01", "Deposition tool in bay one"),
            ]),
            workLine("nw-etch", "Northwater wafer etch", "wafer-etch", 4, "Plasma opens the film where the resist allows it.", [
              toolCard("E-01", "Etch tool in bay one"),
            ]),
          ],
          directTools: [toolCard("nw-film-check", "Film check bench", false)],
        },
        parked("nw-warehousing", "Warehousing", "warehousing", "Stores wafers, gases, and spare parts for Northwater Fab."),
        parked("nw-research", "Research and development", "researchAndDevelopment", "Process experiments that are not on the production floor."),
        parked("nw-sales", "Sales", "sales", "Customer orders for Northwater wafers."),
        parked("nw-back-office", "Back office", "backOffice", "Planning, finance, and records for Northwater Fab."),
        parked("nw-other", "Other", "other", "Facilities and site services at Northwater Fab."),
      ],
    },
    {
      id: "ironwood",
      name: "Ironwood Fab",
      purpose: "Run production wafers.",
      description: "The second production plant.",
      timezone: "America/Chicago",
      capabilities: [
        {
          id: "iw-manufacturing",
          name: "Manufacturing",
          type: "manufacturing",
          description: "Wafer strip, clean, deposition, and etch at Ironwood Fab.",
          simulated: true,
          lines: [
            flowLine("iw-end-to-end", "Ironwood wafer flow", [
              "Ironwood photoresist strip",
              "Ironwood clean",
              "Ironwood thin-film deposition",
              "Ironwood wafer etch",
            ]),
            workLine("iw-deposition", "Ironwood thin-film deposition", "thin-film-deposition", 3, "A chemical vapor deposit grows the film.", [
              toolCard("I-D-01", "Ironwood deposition tool, first"),
            ]),
          ],
          directTools: [],
        },
        parked("iw-warehousing", "Warehousing", "warehousing", "Stores wafers, gases, and spare parts for Ironwood Fab."),
        parked("iw-research", "Research and development", "researchAndDevelopment", "Process experiments that are not on the Ironwood production floor."),
        parked("iw-sales", "Sales", "sales", "Customer orders for Ironwood wafers."),
        parked("iw-back-office", "Back office", "backOffice", "Planning, finance, and records for Ironwood Fab."),
        parked("iw-other", "Other", "other", "Facilities and site services at Ironwood Fab."),
      ],
    },
    {
      id: "lumen",
      name: "Lumen Pilot Works",
      purpose: "Run research wafers.",
      description: "The pilot plant.",
      timezone: "America/Los_Angeles",
      capabilities: [
        {
          id: "lm-manufacturing",
          name: "Manufacturing",
          type: "manufacturing",
          description: "Wafer strip, clean, deposition, and etch at Lumen Pilot Works.",
          simulated: true,
          lines: [
            flowLine("lm-end-to-end", "Lumen wafer flow", [
              "Lumen photoresist strip",
              "Lumen clean",
              "Lumen deposition leg",
              "Lumen wafer etch",
            ]),
            workLine("lm-deposition", "Lumen deposition leg", "thin-film-deposition", 3, "A chemical vapor deposit grows the film before etch.", [
              toolCard("P-D-01", "Lumen deposition tool, first"),
            ]),
          ],
          directTools: [],
        },
        parked("lm-warehousing", "Warehousing", "warehousing", "Stores wafers, gases, and spare parts for the pilot works."),
        parked("lm-research", "Research and development", "researchAndDevelopment", "Research planning for the pilot works. The pilot tools sit under manufacturing."),
        parked("lm-sales", "Sales", "sales", "Customer orders for pilot wafers."),
        parked("lm-back-office", "Back office", "backOffice", "Planning, finance, and records for the pilot works."),
        parked("lm-other", "Other", "other", "Facilities and site services at the pilot works."),
      ],
    },
  ];
}

const metadataRules: BusinessRule[] = [
  {
    id: "rule-critical-human",
    name: "Critical signal needs a person",
    order: 10,
    whenPlain: "When the severity is critical.",
    thenPlain: "Open a ticket, set the priority to critical, send it to the shift lead, ask a person to review it, and start an investigation.",
    why: "A critical signal can affect the wafers or the tool, so a person reviews it before any corrective action.",
    actions: [
      { type: "open-ticket" },
      { type: "set-priority", priority: "critical" },
      { type: "route-to-queue", queue: "Shift lead" },
      { type: "escalate-to-human" },
      { type: "auto-investigate" },
    ],
  },
  {
    id: "rule-sc1-edge-ring",
    name: "Edge ring temperature on the second etch tool",
    order: 110,
    whenPlain: "When a high alert is on chuck temperature at the etch tool in bay two.",
    thenPlain: "Open a ticket, set the priority to high, send it to process engineering, and start an investigation.",
    why: "That pattern matches an edge ring or an engineering change on one chamber. A person is not required yet.",
    actions: [
      { type: "open-ticket" },
      { type: "set-priority", priority: "high" },
      { type: "route-to-queue", queue: "Process engineering" },
      { type: "auto-investigate" },
    ],
  },
];

const metadataMaps: ProcessMap[] = [
  {
    id: "current-quality-path",
    name: "How a quality issue moves today",
    state: "current",
    steps: [
      { id: "notice", name: "Someone notices the excursion", order: 1, highlightStages: [] },
      { id: "search", name: "Search each system", order: 2, highlightStages: [] },
      { id: "email", name: "Hand off by email", order: 3, highlightStages: [] },
      { id: "draft", name: "Draft a cause", order: 4, highlightStages: [] },
      { id: "ask", name: "Ask a manager", order: 5, highlightStages: [] },
      { id: "write", name: "Write the corrective action", order: 6, highlightStages: [] },
    ],
    transitions: [
      { from: "notice", to: "search", name: "Then" },
      { from: "search", to: "email", name: "Then" },
      { from: "search", to: "ask", name: "The records disagree, so the engineer stops and asks." },
      { from: "email", to: "draft", name: "Then" },
      { from: "draft", to: "ask", name: "Then" },
      { from: "ask", to: "write", name: "Then" },
    ],
  },
  {
    id: "future-quality-path",
    name: "How a quality issue moves with rules",
    state: "future",
    steps: [
      { id: "rule", name: "A rule opens the ticket", order: 1, highlightStages: ["ticketOpened"] },
      { id: "investigate", name: "The investigation gathers evidence", order: 2, highlightStages: ["investigating"] },
      { id: "rank", name: "A ranked cause is ready", order: 3, highlightStages: ["recommendationReady"] },
      {
        id: "person",
        name: "A person decides",
        order: 4,
        detail: "An escalated case cannot skip this step.",
        highlightStages: ["escalated", "awaitingApproval", "rejected"],
      },
      { id: "action", name: "The corrective action opens", order: 5, highlightStages: ["approved", "closed"] },
    ],
    transitions: [
      { from: "rule", to: "investigate", name: "Then" },
      { from: "investigate", to: "rank", name: "The evidence is enough" },
      { from: "investigate", to: "person", name: "The evidence is thin or the records disagree" },
      { from: "rank", to: "person", name: "Then" },
      { from: "person", to: "action", name: "The person approves" },
      { from: "person", to: "investigate", name: "The person asks for more evidence." },
    ],
  },
];

/** Live queue only. Scripted S1 and S2 records stay available by id for a direct link. */
function allAlerts(): Alert[] {
  return mockExtras().alerts;
}

function allTickets(): Ticket[] {
  return mockExtras().tickets;
}

function allIncidents(): Incident[] {
  return mockExtras().incidents;
}

function knownIncidents(): Incident[] {
  return [...mockExtras().incidents, ...incidents];
}

function allInvestigations(): Investigation[] {
  return [...clone(investigations), ...mockExtras().investigations];
}

function siteParam(request: Request): string | null {
  return new URL(request.url).searchParams.get("siteId");
}

function newestFirst<T>(rows: T[], time: (row: T) => string): T[] {
  return [...rows].sort((left, right) => time(right).localeCompare(time(left)));
}

const FAMILIES = new Set(["deposition", "etch", "strip", "clean"]);

async function uploadName(request: Request): Promise<string> {
  // Read the raw body. request.formData() can hang under jsdom on Node 22,
  // and the multipart header already carries the filename.
  // Under jsdom on Node 22 the multipart body stream never ends, so give up after a short wait.
  const body = await Promise.race([
    request.arrayBuffer(),
    new Promise<null>((resolve) => setTimeout(() => resolve(null), 750)),
  ]);
  if (body === null) return "oscilloscope-e01.pdf";
  const text = new TextDecoder("latin1").decode(body);
  // jsdom's fetch stringifies FormData, so the unit test cannot carry a filename.
  // That test uploads the oscilloscope sample. A browser sends the real name.
  if (text === "[object FormData]") return "oscilloscope-e01.pdf";
  const named = /filename="([^"]+)"/i.exec(text) ?? /filename=([^;\r\n]+)/i.exec(text);
  return (named?.[1] ?? "").trim().toLowerCase();
}

function catalogParameterFor(toolId: string, parameter: string): string {
  const etch = toolId.startsWith("E-") || toolId.includes("-E-");
  return etch && parameter === "rfPower" ? "rfSourcePower" : parameter;
}

function inspectionForFile(name: string): PrintoutInspection | null {
  if (name.includes("oscilloscope")) {
    return {
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
    };
  }
  if (name.includes("rf-power-meter")) {
    return {
      toolId: "E-02",
      chamberId: "E-02-A",
      observedAt: "2026-10-12T15:05:00Z",
      instrument: "rfPowerMeter",
      parameter: "rfBiasPower",
      value: 510,
      unit: "W",
      specMinimum: 200,
      specMaximum: 500,
      shape: "step",
      operatorInitials: "MR",
      parseMethod: "text",
      outOfSpec: true,
    };
  }
  if (name.includes("particle-counter")) {
    return {
      toolId: "D-01",
      chamberId: "D-01-A",
      observedAt: "2026-10-11T18:10:00Z",
      instrument: "particleCounter",
      parameter: "particlesAdders",
      value: 3,
      unit: "count",
      specMinimum: 0,
      specMaximum: 10,
      shape: null,
      operatorInitials: "BN",
      parseMethod: "text",
      outOfSpec: false,
    };
  }
  if (name.includes("thermocouple")) {
    return {
      toolId: "E-01",
      chamberId: "E-01-A",
      observedAt: "2026-10-12T09:40:00Z",
      instrument: "thermocoupleLogger",
      parameter: "escTemperature",
      value: 68,
      unit: "C",
      specMinimum: 40,
      specMaximum: 60,
      shape: "ramp",
      operatorInitials: "JL",
      parseMethod: name.endsWith(".png") ? "model" : "text",
      outOfSpec: true,
    };
  }
  return null;
}

export const handlers = [
  http.get("/api/sites", () => HttpResponse.json(siteSummaries())),

  http.get("/api/sites/compare", ({ request }) => {
    const family = new URL(request.url).searchParams.get("family") ?? "";
    if (!FAMILIES.has(family)) {
      return problem("Query family must be deposition, etch, strip, or clean.", 400);
    }
    const comparison = familyComparison(family);
    if (!comparison) return problem("Query family must be deposition, etch, strip, or clean.", 400);
    return HttpResponse.json(comparison);
  }),

  http.get("/api/sites/:siteId", ({ params }) => {
    const site = siteDetail(String(params.siteId));
    if (!site) return problem("Plant was not found.", 404);
    return HttpResponse.json(site);
  }),

  http.get("/api/tools", ({ request }) => {
    const plant = requestedPlant(request);
    return HttpResponse.json(clone(tools.filter((tool) => toolInPlant(plant, tool.id, tool.siteId))));
  }),

  http.get("/api/tools/:toolId", ({ params }) => {
    const toolId = String(params.toolId);
    const tool = tools.find((item) => item.id === toolId) ?? toolRecord(toolId);
    if (!tool) return problem("Tool was not found.", 404);
    return HttpResponse.json(clone(tool));
  }),

  http.get("/api/tools/:toolId/telemetry", ({ params }) => {
    const toolId = String(params.toolId);
    const faulted = allAlerts().some((alert) => alert.toolId === toolId);
    const rows = telemetryForTool(toolId, { applyDemoFault: faulted });
    if (rows.length === 0) return problem("Tool was not found.", 404);
    return HttpResponse.json(rows);
  }),

  http.get("/api/fleet/live", () => HttpResponse.json({ simulatedNow: "2026-09-01T00:02:00Z", tools: [] })),

  http.get("/api/tools/:toolId/live", ({ params }) =>
    HttpResponse.json({
      toolId: String(params.toolId),
      siteId: "northwater",
      simulatedNow: "2026-09-01T00:02:00Z",
      status: "nominal",
      readings: [],
      series: [],
    }),
  ),

  http.get("/api/alerts", ({ request }) => {
    const url = new URL(request.url);
    const toolId = url.searchParams.get("toolId");
    const siteId = siteParam(request);
    const rows = newestFirst(
      allAlerts().filter((alert) => (toolId ? alert.toolId === toolId : true) && toolInPlant(siteId ?? "all", alert.toolId)),
      (alert) => alert.raisedAt,
    );
    return HttpResponse.json(rows);
  }),

  http.get("/api/tickets", ({ request }) => {
    const siteId = siteParam(request);
    const rows = newestFirst(
      allTickets().filter((ticket) => toolInPlant(siteId ?? "all", ticket.toolId)),
      (ticket) => ticket.openedAt,
    );
    return HttpResponse.json(rows);
  }),

  http.get("/api/incidents", ({ request }) => {
    const plant = requestedPlant(request);
    return HttpResponse.json(allIncidents().filter((incident) => toolInPlant(plant, incident.toolId)));
  }),

  http.get("/api/incidents/:incidentId", ({ params }) => {
    const incident = knownIncidents().find((item) => item.id === params.incidentId);
    if (!incident) return problem("Incident was not found.", 404);
    return HttpResponse.json(incident);
  }),

  http.get("/api/incidents/:incidentId/investigation", ({ params }) => {
    const incidentId = String(params.incidentId);
    const latest = latestInvestigationForIncident(incidentId, [...investigations, ...mockExtras().investigations]);
    if (!latest) return problem("No such record.", 404);
    return HttpResponse.json(clone(latest));
  }),

  http.post("/api/incidents", async ({ request }) => {
    const body = (await request.json()) as OpenIncidentRequest;
    if (!body.toolId || !body.parameter || !body.summary) {
      return problem("Tool, parameter, and summary are required.", 400);
    }
    const open = knownIncidents().find(
      (item) =>
        item.toolId === body.toolId &&
        item.parameter === body.parameter &&
        item.stage !== "closed" &&
        item.stage !== "closedByDemoReset" &&
        item.stage !== "rejected",
    );
    if (open) return HttpResponse.json(open, { status: 201 });

    const id = crypto.randomUUID();
    const openedAt = new Date().toISOString();
    const incident: Incident = {
      id,
      toolId: body.toolId,
      chamberId: body.chamberId ?? null,
      parameter: body.parameter,
      channel: "operatorReport",
      severity: body.severity ?? "medium",
      summary: body.summary,
      correlationId: `corr-op-${id.slice(0, 8)}`,
      openedAt,
      stage: "ticketOpened",
    };
    const ticket: Ticket = {
      id: crypto.randomUUID(),
      number: `TKT-${1100 + mockExtras().tickets.length}`,
      incidentId: id,
      toolId: body.toolId,
      title: body.summary,
      openedAt,
    };
    mockExtras().incidents.push(incident);
    mockExtras().tickets.push(ticket);
    return HttpResponse.json(incident, { status: 201 });
  }),

  http.get("/api/investigations", ({ request }) => {
    const siteId = siteParam(request);
    const incidentById = new Map(allIncidents().map((incident) => [incident.id, incident]));
    const rows = newestFirst(
      allInvestigations().filter((investigation) => matchesPlant(siteId, incidentById.get(investigation.incidentId)?.toolId)),
      (investigation) => incidentById.get(investigation.incidentId)?.openedAt ?? "",
    );
    return HttpResponse.json(rows);
  }),

  http.get("/api/investigations/:investigationId", ({ params }) => {
    const id = String(params.investigationId);
    const found = mockExtras().investigations.find((item) => item.id === id) ?? investigationFor(id);
    if (!found) return problem("No such record.", 404);
    return HttpResponse.json(clone(found));
  }),

  http.post("/api/incidents/:incidentId/investigations", ({ params }) => {
    const incidentId = String(params.incidentId);
    const incident = knownIncidents().find((item) => item.id === incidentId);
    if (!incident) return problem("Incident was not found.", 404);
    const existing =
      mockExtras().investigations.find((item) => item.incidentId === incidentId) ??
      investigations.find((item) => item.incidentId === incidentId);
    if (existing) return problem("An investigation is already open for this incident.", 409);

    const created = {
      id: crypto.randomUUID(),
      incidentId,
      stage: "started" as const,
      evidence: [],
      hypotheses: [],
      escalationReason: null,
      correlationId: incident.correlationId,
    };
    mockExtras().investigations.push(created);
    const stored = knownIncidents().find((item) => item.id === incidentId);
    if (stored && mockExtras().incidents.includes(stored)) stored.stage = "investigating";
    return HttpResponse.json(created, { status: 201 });
  }),

  http.post("/api/faults", async ({ request }) => {
    const body = (await request.json()) as FaultInjectionRequest;
    if (!body.toolId || !body.parameter || !body.shape || body.magnitude === undefined || !body.rootCauseCategory) {
      return problem("A fault needs a tool, parameter, shape, magnitude, and category.", 400);
    }
    const faultId = crypto.randomUUID();
    const correlationId = `corr-inject-${faultId.slice(0, 8)}`;
    // The stream raises the alert on a later tick. This click only accepts the fault.
    return HttpResponse.json({ faultId, correlationId }, { status: 202 });
  }),

  http.post("/api/printouts/inspect", async ({ request }) => {
    const name = await uploadName(request);
    const reading = inspectionForFile(name);
    if (!reading) return problem("The printout could not be read.", 400);
    return HttpResponse.json(reading);
  }),

  http.get("/api/manual-readings", () => HttpResponse.json([])),
  http.get("/api/manual-readings/:evidenceId/file", () => new HttpResponse(null, { status: 404 })),
  http.post("/api/printouts/confirm", async ({ request }) => {
    const body = (await request.json()) as PrintoutConfirmRequest;
    const outOfSpec = body.value < body.specMinimum || body.value > body.specMaximum;
    const catalogParameter = catalogParameterFor(body.toolId, body.parameter);
    const correlationId = `corr-printout-${crypto.randomUUID().slice(0, 8)}`;
    const incidentId = crypto.randomUUID();
    const raisedAt = body.observedAt || new Date().toISOString();
    const summary = `Manual measurement: ${body.instrument} on ${body.toolId}, ${body.parameter} ${body.value} ${body.unit}.`;
    const incident: Incident = {
      id: incidentId,
      toolId: body.toolId,
      chamberId: body.chamberId ?? null,
      parameter: catalogParameter,
      channel: "operatorReport",
      severity: outOfSpec ? "high" : "low",
      summary,
      correlationId,
      openedAt: raisedAt,
      stage: "ticketOpened",
    };
    const ticket: Ticket = {
      id: crypto.randomUUID(),
      number: `TKT-${1300 + mockExtras().tickets.length}`,
      incidentId,
      toolId: body.toolId,
      title: summary,
      openedAt: raisedAt,
    };
    mockExtras().incidents.push(incident);
    mockExtras().tickets.push(ticket);
    let alertId: string | null = null;
    if (outOfSpec) {
      alertId = crypto.randomUUID();
      const alert: Alert = {
        id: alertId,
        toolId: body.toolId,
        chamberId: body.chamberId ?? null,
        parameter: catalogParameter,
        severity: "high",
        rule: "manualMeasurement",
        raisedAt,
        correlationId,
      };
      mockExtras().alerts.push(alert);
    }
    return HttpResponse.json({
      evidenceId: crypto.randomUUID(),
      incidentId,
      faultId: outOfSpec ? crypto.randomUUID() : null,
      correlationId: outOfSpec ? correlationId : null,
      alertId,
      appliedToSimulation: outOfSpec,
      outOfSpec,
      printedParameter: body.parameter,
      catalogParameter,
    });
  }),

  http.post("/api/injector/stop", () => HttpResponse.json({ removed: 0 })),
  http.post("/api/injector/scenarios/:scenarioId", ({ params }) => {
    const scenarioId = String(params.scenarioId);
    const run: ScenarioRun = {
      scenarioId,
      seed: 4242,
      faultId: crypto.randomUUID(),
      correlationId: `corr-scenario-${scenarioId}`,
      telemetry: [],
    };
    return HttpResponse.json(run, { status: 202 });
  }),

  http.get("/api/audit", () => HttpResponse.json({ entries: [], traces: [] })),

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
    if (kind === "hard") resetMockState();
    return HttpResponse.json({
      seed: 4242,
      simulatedOrigin: "2026-09-01T00:00:00Z",
      fleetToolCount: 8,
      ticketCount: 0,
      investigationCount: 0,
      approvalCount: 0,
      capaCount: 0,
      telemetryCount: kind === "hard" ? 12000 : 8,
      eventCount: 1,
      resetEventId: "reset-event",
      kind,
    });
  }),

  http.get("/api/demo/speed", () => HttpResponse.json({ speed: 1, strideSeconds: 15, speeds: [1, 2, 5, 10], strides: [5, 15, 30, 60] })),
  http.post("/api/demo/speed", async ({ request }) => {
    const body = (await request.json()) as { speed: number; strideSeconds: number };
    return HttpResponse.json({ ...body, speeds: [1, 2, 5, 10], strides: [5, 15, 30, 60] });
  }),
  http.get("/api/demo/data-mode", () => HttpResponse.json({ mode: "live", scenarioId: null, messageCount: 0 })),

  http.post("/api/demo/data-mode", async ({ request }) => {
    const body = (await request.json()) as { mode?: string; scenarioId?: string };
    if (body.mode === "live") return HttpResponse.json({ mode: "live", scenarioId: null, messageCount: 0 });
    if (body.mode === "replay" && body.scenarioId) {
      return HttpResponse.json({ mode: "replay", scenarioId: body.scenarioId, messageCount: 12 });
    }
    return problem("That scenario has no recording.", 404);
  }),


  http.post("/api/investigations/:investigationId/decisions", async ({ params, request }) => {
    const body = (await request.json()) as { kind?: string; approverId?: string; comment?: string | null };
    if (body.kind === "override" && (body.comment ?? "").trim().length < 20) {
      return problem("Override needs a written justification of at least 20 characters.", 400);
    }
    if ((body.kind === "approve" || body.kind === "override") && !body.approverId) {
      return problem("Approval requires an approver.", 400);
    }
    const id = String(params.investigationId);
    const current = mockExtras().investigations.find((item) => item.id === id) ?? investigationFor(id);
    if (!current) return problem("No such record.", 404);
    const next = clone(current);
    next.stage =
      body.kind === "approve" || body.kind === "override" ? "approved" : body.kind === "reject" ? "rejected" : "escalated";
    mockExtras().investigations.push(next);
    const capa =
      body.kind === "approve" || body.kind === "override"
        ? {
            id: "66666666-6666-4666-8666-666666666666",
            incidentId: current.incidentId,
            summary: "Hold the affected lots and confirm the edge ring revision.",
            approvalTokenId: "77777777-7777-4777-8777-777777777777",
            actionHash: "ABC123",
            stage: "open" as const,
            openedAt: "2026-10-14T15:05:00Z",
            approvedBy: body.approverId ?? null,
            approvedAt: "2026-10-15T02:42:00.000Z",
          }
        : null;
    return HttpResponse.json({ investigation: next, capa });
  }),

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
  http.get("/api/metadata/sites", () => HttpResponse.json(metadataSites())),
  http.get("/api/metadata/tools/:toolId", () =>
    HttpResponse.json({
      id: "E-01",
      name: "Etch tool in bay one",
      toolType: "Conductor etch",
      siteId: "northwater",
      siteName: "Northwater Fab",
      lineId: "nw-etch",
      lineName: "Northwater wafer etch",
      capabilityId: "nw-manufacturing",
      simulated: true,
      inputs: [{ name: "Patterned wafer", description: "A wafer with resist still on the film." }],
      outputs: [{ name: "Etched wafer", description: "A wafer with the film opened." }],
      parameters: [
        {
          id: "escTemperature",
          name: "Chuck temperature",
          parameterClass: "temperature",
          unit: "C",
          lcl: 40,
          ucl: 60,
          alertLow: 42,
          alertHigh: 58,
          nominal: 50,
          telemetry: true,
        },
      ],
    }),
  ),
  http.get("/api/metadata/rules", () => HttpResponse.json(metadataRules)),
  http.get("/api/metadata/process-maps", () => HttpResponse.json(metadataMaps)),

  ...contractHandlers,
];
