// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company
import type {
  AdvanceCapaRequest,
  Alert,
  Capa,
  DecisionResult,
  AdminSession,
  AuditLog,
  DemoResetResult,
  FaultInjectionAccepted,
  FaultInjectionRequest,
  FamilyComparison,
  Health,
  ScenarioRun,
  ScriptedScenario,
  HumanDecisionRequest,
  Incident,
  Investigation,
  MetricsSummary,
  SiteDetail,
  SiteSummary,
  OpenIncidentRequest,
  Problem,
  Ticket,
  Tool,
} from "./types";

const base = import.meta.env.VITE_API_BASE ?? "";

export class ApiProblem extends Error {
  readonly status: number;
  readonly title: string;
  readonly detail?: string;

  constructor(status: number, title: string, detail?: string) {
    super(detail && detail.length > 0 ? detail : title);
    this.name = "ApiProblem";
    this.status = status;
    this.title = title;
    this.detail = detail;
  }
}

export async function apiSend<T>(path: string, init?: RequestInit): Promise<T> {
  const headers = new Headers(init?.headers);
  headers.set("accept", "application/json");
  if (init?.body !== undefined && !headers.has("content-type")) {
    headers.set("content-type", "application/json");
  }

  const response = await fetch(`${base}${path}`, { ...init, headers });
  if (!response.ok) {
    let title = response.statusText || "Request failed";
    let detail: string | undefined;
    try {
      const problem = (await response.json()) as Problem;
      if (problem.title && problem.title.length > 0) title = problem.title;
      if (problem.detail && problem.detail.length > 0) detail = problem.detail;
    } catch {
      // The body was not problem+json.
    }
    throw new ApiProblem(response.status, title, detail);
  }

  if (response.status === 204) return undefined as T;
  return (await response.json()) as T;
}

function listPath(path: string, siteId?: string, extra?: Record<string, string | undefined>): string {
  const params = new URLSearchParams();
  if (extra) {
    for (const [key, value] of Object.entries(extra)) {
      if (value) params.set(key, value);
    }
  }
  if (siteId && siteId !== "all") params.set("siteId", siteId);
  const query = params.toString();
  return query ? `${path}?${query}` : path;
}

export const api = {
  health: () => apiSend<Health>("/health"),
  listSites: () => apiSend<SiteSummary[]>("/api/sites"),
  getSite: (siteId: string) => apiSend<SiteDetail>(`/api/sites/${encodeURIComponent(siteId)}`),
  compareSites: (family: string) =>
    apiSend<FamilyComparison>(`/api/sites/compare?family=${encodeURIComponent(family)}`),
  listTools: (siteId?: string) => apiSend<Tool[]>(listPath("/api/tools", siteId)),
  getTool: (toolId: string) => apiSend<Tool>(`/api/tools/${encodeURIComponent(toolId)}`),
  listAlerts: (toolId?: string, siteId?: string) => apiSend<Alert[]>(listPath("/api/alerts", siteId, { toolId })),
  listTickets: (siteId?: string) => apiSend<Ticket[]>(listPath("/api/tickets", siteId)),
  listIncidents: (siteId?: string) => apiSend<Incident[]>(listPath("/api/incidents", siteId)),
  getIncident: (incidentId: string) => apiSend<Incident>(`/api/incidents/${incidentId}`),
  openIncident: (body: OpenIncidentRequest) =>
    apiSend<Incident>("/api/incidents", { method: "POST", body: JSON.stringify(body) }),
  getInvestigation: (investigationId: string) =>
    apiSend<Investigation>(`/api/investigations/${investigationId}`),
  getLatestInvestigation: (incidentId: string) =>
    apiSend<Investigation>(`/api/incidents/${incidentId}/investigation`),
  startInvestigation: (incidentId: string) =>
    apiSend<Investigation>(`/api/incidents/${incidentId}/investigations`, { method: "POST" }),
  submitDecision: (investigationId: string, body: HumanDecisionRequest) =>
    apiSend<DecisionResult>(`/api/investigations/${investigationId}/decisions`, {
      method: "POST",
      body: JSON.stringify(body),
    }),
  listCapas: (siteId?: string) => apiSend<Capa[]>(listPath("/api/capas", siteId)),
  getCapa: (capaId: string) => apiSend<Capa>(`/api/capas/${capaId}`),
  advanceCapa: (capaId: string, body: AdvanceCapaRequest) =>
    apiSend<Capa>(`/api/capas/${capaId}/advance`, { method: "POST", body: JSON.stringify(body) }),
  injectFault: (body: FaultInjectionRequest) =>
    apiSend<FaultInjectionAccepted>("/api/faults", { method: "POST", body: JSON.stringify(body) }),
  getAdminSession: () => apiSend<AdminSession>("/api/admin/session"),
  getAuditLog: () => apiSend<AuditLog>("/api/audit"),
  resetDemo: () => apiSend<DemoResetResult>("/api/admin/reset", { method: "POST" }),
  listScenarios: () => apiSend<ScriptedScenario[]>("/api/injector/scenarios"),
  runScenario: (scenarioId: string) =>
    apiSend<ScenarioRun>(`/api/injector/scenarios/${encodeURIComponent(scenarioId)}`, { method: "POST" }),
  getMetrics: () => apiSend<MetricsSummary>("/api/metrics"),
};
