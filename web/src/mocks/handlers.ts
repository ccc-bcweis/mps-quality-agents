// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company
// Browser mocks. List routes use the demo fleet. Decision, CAPA, health, and
// metrics routes fall through to contracts/msw/handlers.ts.

import { http, HttpResponse } from "msw";
import { handlers as contractHandlers } from "../../../contracts/msw/handlers";
import type { Alert, FaultInjectionRequest, Incident, OpenIncidentRequest, PrintoutConfirmRequest, PrintoutInspection, ScenarioRun, Ticket } from "../api/types";
import { telemetryForTool } from "../fleet/telemetry";
import { latestInvestigationForIncident } from "../investigation/ticketRoute";
import { alerts, incidents, investigationFor, investigations, tickets, tools } from "./demoData";
import { familyComparison, siteDetail, siteSummaries, toolRecord } from "./sites/fixtures";
import { mockExtras } from "./state";

function problem(title: string, status: number) {
  return HttpResponse.json(
    { title, status },
    { status, headers: { "content-type": "application/problem+json" } },
  );
}

function clone<T>(value: T): T {
  return structuredClone(value);
}

function allAlerts(): Alert[] {
  return [...clone(alerts), ...mockExtras().alerts];
}

function allTickets(): Ticket[] {
  return [...clone(tickets), ...mockExtras().tickets];
}

function allIncidents(): Incident[] {
  return [...clone(incidents), ...mockExtras().incidents];
}

const FAMILIES = new Set(["deposition", "etch", "strip", "clean"]);

async function uploadName(request: Request): Promise<string> {
  const contentType = request.headers.get("content-type") ?? "";
  if (contentType.includes("multipart/form-data") || contentType.includes("application/x-www-form-urlencoded")) {
    const form = await request.formData();
    const file = form.get("file");
    if (file instanceof File) return file.name.toLowerCase();
  }
  const text = new TextDecoder("latin1").decode(await request.arrayBuffer());
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

  http.get("/api/tools", () => HttpResponse.json(clone(tools))),

  http.get("/api/tools/:toolId", ({ params }) => {
    const toolId = String(params.toolId);
    const tool = tools.find((item) => item.id === toolId) ?? toolRecord(toolId);
    if (!tool) return problem("Tool was not found.", 404);
    return HttpResponse.json(clone(tool));
  }),

  http.get("/api/tools/:toolId/telemetry", ({ params }) => {
    const toolId = String(params.toolId);
    const rows = telemetryForTool(toolId);
    if (rows.length === 0) return problem("Tool was not found.", 404);
    return HttpResponse.json(rows);
  }),

  http.get("/api/alerts", ({ request }) => {
    const toolId = new URL(request.url).searchParams.get("toolId");
    const rows = allAlerts().filter((alert) => (toolId ? alert.toolId === toolId : true));
    return HttpResponse.json(rows);
  }),

  http.get("/api/tickets", () => HttpResponse.json(allTickets())),

  http.get("/api/incidents", () => HttpResponse.json(allIncidents())),

  http.get("/api/incidents/:incidentId", ({ params }) => {
    const incident = allIncidents().find((item) => item.id === params.incidentId);
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
    const open = allIncidents().find(
      (item) =>
        item.toolId === body.toolId &&
        item.parameter === body.parameter &&
        item.stage !== "closed" &&
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

  http.get("/api/investigations/:investigationId", ({ params }) => {
    const id = String(params.investigationId);
    const found = mockExtras().investigations.find((item) => item.id === id) ?? investigationFor(id);
    if (!found) return problem("No such record.", 404);
    return HttpResponse.json(clone(found));
  }),

  http.post("/api/incidents/:incidentId/investigations", ({ params }) => {
    const incidentId = String(params.incidentId);
    const incident = allIncidents().find((item) => item.id === incidentId);
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
    const stored = allIncidents().find((item) => item.id === incidentId);
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
    const raisedAt = new Date().toISOString();
    const incidentId = crypto.randomUUID();
    const alert: Alert = {
      id: crypto.randomUUID(),
      toolId: body.toolId,
      chamberId: body.chamberId ?? null,
      parameter: body.parameter,
      severity: "high",
      rule: "westernElectric1",
      raisedAt,
      correlationId,
    };
    const incident: Incident = {
      id: incidentId,
      toolId: body.toolId,
      chamberId: body.chamberId ?? null,
      parameter: body.parameter,
      channel: "faultInjector",
      severity: "high",
      summary: `Injected ${body.shape} on ${body.parameter} (${body.magnitude} σ).`,
      correlationId,
      openedAt: raisedAt,
      stage: "ticketOpened",
    };
    const ticket: Ticket = {
      id: crypto.randomUUID(),
      number: `TKT-${1200 + mockExtras().tickets.length}`,
      incidentId,
      toolId: body.toolId,
      title: incident.summary,
      openedAt: raisedAt,
    };
    mockExtras().alerts.push(alert);
    mockExtras().incidents.push(incident);
    mockExtras().tickets.push(ticket);
    return HttpResponse.json({ faultId, correlationId }, { status: 202 });
  }),

  http.post("/api/printouts/inspect", async ({ request }) => {
    const name = await uploadName(request);
    const reading = inspectionForFile(name);
    if (!reading) return problem("The printout could not be read.", 400);
    return HttpResponse.json(reading);
  }),

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

  http.post("/api/injector/scenarios/:scenarioId", ({ params }) => {
    const scenarioId = String(params.scenarioId);
    const correlationId = `corr-scenario-${scenarioId}`;
    const raisedAt = new Date().toISOString();
    const alert: Alert = {
      id: crypto.randomUUID(),
      toolId: scenarioId === "SC-1" ? "E-02" : "E-01",
      chamberId: scenarioId === "SC-1" ? "E-02-A" : "E-01-A",
      parameter: scenarioId === "SC-6" ? "rfSourcePower" : "escTemperature",
      severity: "high",
      rule: "westernElectric1",
      raisedAt,
      correlationId,
    };
    mockExtras().alerts.push(alert);
    const run: ScenarioRun = {
      scenarioId,
      seed: 4242,
      faultId: crypto.randomUUID(),
      correlationId,
      telemetry: [],
    };
    return HttpResponse.json(run, { status: 202 });
  }),

  ...contractHandlers,
];
