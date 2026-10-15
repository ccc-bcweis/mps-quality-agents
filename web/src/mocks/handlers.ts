// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company
// Browser mocks. List routes use the demo fleet. Decision, CAPA, health, and
// metrics routes fall through to contracts/msw/handlers.ts.

import { http, HttpResponse } from "msw";
import { handlers as contractHandlers } from "../../../contracts/msw/handlers";
import type { Alert, FaultInjectionRequest, Incident, Investigation, OpenIncidentRequest, ScenarioRun, Ticket } from "../api/types";
import { telemetryForTool } from "../fleet/telemetry";
import { latestInvestigationForIncident } from "../investigation/ticketRoute";
import { alerts, incidents, investigationFor, investigations, tickets, tools } from "./demoData";
import { matchesPlant } from "./plants";
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
    const url = new URL(request.url);
    const toolId = url.searchParams.get("toolId");
    const siteId = siteParam(request);
    const rows = newestFirst(
      allAlerts().filter((alert) => (toolId ? alert.toolId === toolId : true) && matchesPlant(siteId, alert.toolId)),
      (alert) => alert.raisedAt,
    );
    return HttpResponse.json(rows);
  }),

  http.get("/api/tickets", ({ request }) => {
    const siteId = siteParam(request);
    const rows = newestFirst(
      allTickets().filter((ticket) => matchesPlant(siteId, ticket.toolId)),
      (ticket) => ticket.openedAt,
    );
    return HttpResponse.json(rows);
  }),

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

  http.post("/api/injector/scenarios/:scenarioId", ({ params }) => {
    const scenarioId = String(params.scenarioId);
    const toolId = scenarioId === "SC-1" ? "E-02" : "E-01";
    const correlationId = `corr-scenario-${scenarioId}`;
    const raisedAt = new Date().toISOString();
    const incidentId = crypto.randomUUID();
    const title = `Scenario ${scenarioId} on ${toolId}.`;
    const alert: Alert = {
      id: crypto.randomUUID(),
      toolId,
      chamberId: `${toolId}-A`,
      parameter: "escTemperature",
      severity: "high",
      rule: "westernElectric1",
      raisedAt,
      correlationId,
    };
    const incident: Incident = {
      id: incidentId,
      toolId,
      chamberId: `${toolId}-A`,
      parameter: "escTemperature",
      channel: "faultInjector",
      severity: "high",
      summary: title,
      correlationId,
      openedAt: raisedAt,
      stage: "investigating",
    };
    const ticket: Ticket = {
      id: crypto.randomUUID(),
      number: `TKT-${1300 + mockExtras().tickets.length}`,
      incidentId,
      toolId,
      title,
      openedAt: raisedAt,
    };
    const created: Investigation = {
      id: crypto.randomUUID(),
      incidentId,
      stage: "started",
      evidence: [],
      hypotheses: [],
      escalationReason: null,
      correlationId,
    };
    mockExtras().alerts.push(alert);
    mockExtras().incidents.push(incident);
    mockExtras().tickets.push(ticket);
    mockExtras().investigations.push(created);
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
