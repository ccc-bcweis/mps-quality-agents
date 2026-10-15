// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company
// Browser mocks. List routes use the demo fleet. Health, metrics, and the
// CAPA advance route fall through to contracts/msw/handlers.ts.

import { http, HttpResponse } from "msw";
import { handlers as contractHandlers } from "../../../contracts/msw/handlers";
import type { Alert, FaultInjectionRequest, Incident, OpenIncidentRequest, ScenarioRun, Ticket } from "../api/types";
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

  http.post("/api/injector/scenarios/:scenarioId", ({ params }) => {
    const scenarioId = String(params.scenarioId);
    const correlationId = `corr-scenario-${scenarioId}`;
    const raisedAt = new Date().toISOString();
    const alert: Alert = {
      id: crypto.randomUUID(),
      toolId: scenarioId === "SC-1" ? "E-02" : "E-01",
      chamberId: scenarioId === "SC-1" ? "E-02-A" : "E-01-A",
      parameter: "escTemperature",
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
          }
        : null;
    return HttpResponse.json({ investigation: next, capa });
  }),

  ...contractHandlers,
];
