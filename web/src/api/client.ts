// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company
import type { AdminSession, AdvanceCapaRequest, Alert, AuditLog, BusinessRule, Capa, CausalTrace, CompanyMetadata, ConsoleCatalog, DataMode, DecisionResult, DemoResetResult, FamilyComparison, FaultInjectionAccepted, FaultInjectionRequest, FleetLive, Health, HumanDecisionRequest, Incident, Investigation, JoinLink, JoinLinks, ManualReading, MetadataSite, MetadataTool, MetricsSummary, ModelStatus, OpenIncidentRequest, PrintoutConfirmRequest, PrintoutConfirmation, PrintoutInspection, Problem, RedeemResult, ResetKind, ResetProgress, RuleSetting, ScenarioRun, ScriptedScenario, SiteDetail, SiteSummary, Ticket, Tool, ToolLive } from "./types";

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

async function apiSend<T>(path: string, init?: RequestInit): Promise<T> {
  const headers = new Headers(init?.headers);
  headers.set("accept", "application/json");
  if (init?.body !== undefined && !(init.body instanceof FormData) && !headers.has("content-type")) {
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
  listFleetLive: () => apiSend<FleetLive>("/api/fleet/live"),
  listManualReadings: (toolId?: string) =>
    apiSend<ManualReading[]>(toolId ? `/api/manual-readings?toolId=${encodeURIComponent(toolId)}` : "/api/manual-readings"),
  getToolLive: (toolId: string) => apiSend<ToolLive>(`/api/tools/${encodeURIComponent(toolId)}/live`),
  listAlerts: (toolId?: string, siteId?: string) => apiSend<Alert[]>(listPath("/api/alerts", siteId, { toolId })),
  listTickets: (siteId?: string) => apiSend<Ticket[]>(listPath("/api/tickets", siteId)),
  listIncidents: (siteId?: string) => apiSend<Incident[]>(listPath("/api/incidents", siteId)),
  getIncident: (incidentId: string) => apiSend<Incident>(`/api/incidents/${incidentId}`),
  openIncident: (body: OpenIncidentRequest) =>
    apiSend<Incident>("/api/incidents", { method: "POST", body: JSON.stringify(body) }),
  listInvestigations: (siteId?: string) => apiSend<Investigation[]>(listPath("/api/investigations", siteId)),
  getInvestigation: (investigationId: string) =>
    apiSend<Investigation>(`/api/investigations/${investigationId}`),
  getLatestInvestigation: (incidentId: string) =>
    apiSend<Investigation>(`/api/incidents/${incidentId}/investigation`),
  /** Every round on a ticket, oldest first. */
  listInvestigationRounds: (incidentId: string) =>
    apiSend<Investigation[]>(`/api/incidents/${incidentId}/investigations`),
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
  inspectPrintout: (file: File) => {
    const body = new FormData();
    body.append("file", file);
    return apiSend<PrintoutInspection>("/api/printouts/inspect", { method: "POST", body });
  },
  confirmPrintout: (body: PrintoutConfirmRequest) =>
    apiSend<PrintoutConfirmation>("/api/printouts/confirm", { method: "POST", body: JSON.stringify(body) }),
  getAdminSession: () => apiSend<AdminSession>("/api/admin/session"),
  listJoinLinks: () => apiSend<JoinLinks>("/api/admin/join-links"),
  createJoinLink: (hours: number, maxUses: number) =>
    apiSend<JoinLink>("/api/admin/join-links", { method: "POST", body: JSON.stringify({ hours, maxUses }) }),
  revokeJoinLink: (id: string) => apiSend<void>(`/api/admin/join-links/${encodeURIComponent(id)}`, { method: "DELETE" }),
  redeemJoin: (token: string) => apiSend<RedeemResult>(`/api/join/${encodeURIComponent(token)}`, { method: "POST" }),
  getAuditLog: () => apiSend<AuditLog>("/api/audit"),
  resetDemo: (kind: ResetKind) =>
    apiSend<DemoResetResult>("/api/admin/reset", { method: "POST", body: JSON.stringify({ kind }) }),
  getSpeed: () => apiSend<DemoSpeed>("/api/demo/speed"),
  setSpeed: (speed: number, strideSeconds: number) =>
    apiSend<DemoSpeed>("/api/demo/speed", { method: "POST", body: JSON.stringify({ speed, strideSeconds }) }),
  getResetProgress: () => apiSend<ResetProgress>("/api/admin/reset/progress"),
  getDataMode: () => apiSend<DataMode>("/api/demo/data-mode"),
  setDataMode: (mode: DataMode["mode"], scenarioId?: string) =>
    apiSend<DataMode>("/api/demo/data-mode", {
      method: "POST",
      body: JSON.stringify(scenarioId ? { mode, scenarioId } : { mode }),
    }),
  companyMetadata: () => apiSend<CompanyMetadata>("/api/metadata/company"),
  metadataSites: () => apiSend<MetadataSite[]>("/api/metadata/sites"),
  metadataTool: (toolId: string) => apiSend<MetadataTool>(`/api/metadata/tools/${encodeURIComponent(toolId)}`),
  businessRules: () => apiSend<BusinessRule[]>("/api/metadata/rules"),
  ruleSettings: () => apiSend<RuleSetting[]>("/api/metadata/settings"),
  consoleCatalog: () => apiSend<ConsoleCatalog>("/api/metadata/catalog"),
  listScenarios: () => apiSend<ScriptedScenario[]>("/api/injector/scenarios"),
  runScenario: (scenarioId: string) =>
    apiSend<ScenarioRun>(`/api/injector/scenarios/${encodeURIComponent(scenarioId)}`, { method: "POST" }),
  stopScenario: () => apiSend<{ removed: number }>("/api/injector/stop", { method: "POST" }),
  getMetrics: () => apiSend<MetricsSummary>("/api/metrics"),
  getModelStatus: () => apiSend<ModelStatus>("/api/model/status"),
  getTrace: (eventId: string) => apiSend<CausalTrace>(`/api/trace/${encodeURIComponent(eventId)}`),
};

export type DemoSpeed = { speed: number; strideSeconds: number; speeds: number[]; strides: number[] };
