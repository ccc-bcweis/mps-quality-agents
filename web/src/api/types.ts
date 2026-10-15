// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company
// Types follow contracts/openapi.yaml. JSON enums stay camelCase.

export type ToolFamily = "deposition" | "etch" | "strip" | "clean";
export type Severity = "low" | "medium" | "high" | "critical";
export type IncidentChannel =
  | "iotSpcAlert"
  | "operatorReport"
  | "qmsNonconformance"
  | "supplierQualityNotice"
  | "faultInjector";
export type IncidentStage =
  | "ticketOpened"
  | "investigating"
  | "recommendationReady"
  | "escalated"
  | "awaitingApproval"
  | "approved"
  | "rejected"
  | "closed";
export type EvidenceSource =
  | "telemetry"
  | "spc"
  | "mes"
  | "qms"
  | "plm"
  | "erp"
  | "cmms"
  | "document"
  | "ticketing";
export type RootCauseCategory =
  | "engineeringChange"
  | "equipmentCalibrationOrAlignment"
  | "supplyChainMaterial"
  | "processDrift";
export type InvestigationStage =
  | "started"
  | "evidenceGathered"
  | "recommendationReady"
  | "escalated"
  | "awaitingApproval"
  | "approved"
  | "rejected"
  | "failed";
export type HumanDecisionKind = "approve" | "reject" | "requestInfo" | "override";
export type CapaStage =
  | "open"
  | "containment"
  | "rootCauseConfirmed"
  | "correctiveAction"
  | "verification"
  | "closed";
export type FaultShape = "step" | "ramp" | "oscillation" | "intermittent";
export type RunMode = "replay" | "live";

export type Problem = {
  type?: string;
  title?: string;
  status?: number;
  detail?: string;
  instance?: string;
};

export type Health = { status: "ok"; service: string };

export type Chamber = { id: string; name: string };

export type Tool = {
  id: string;
  name: string;
  family: ToolFamily;
  model: string;
  bay: string;
  siteId?: string;
  chambers: Chamber[];
};

export type Alert = {
  id: string;
  toolId: string;
  chamberId?: string | null;
  parameter: string;
  severity: Severity;
  rule: string;
  raisedAt: string;
  correlationId: string;
};

export type Ticket = {
  id: string;
  number: string;
  incidentId: string;
  toolId: string;
  title: string;
  openedAt: string;
};

export type Incident = {
  id: string;
  toolId: string;
  chamberId?: string | null;
  parameter: string;
  channel: IncidentChannel;
  severity: Severity;
  summary: string;
  correlationId: string;
  openedAt: string;
  stage: IncidentStage;
};

export type OpenIncidentRequest = {
  toolId: string;
  chamberId?: string | null;
  parameter: string;
  summary: string;
  severity?: Severity;
};

export type EvidenceItem = {
  id: string;
  source: EvidenceSource;
  title: string;
  excerpt: string;
  observedAt: string;
  toolId?: string | null;
  externalRef?: string | null;
  whyItMatters?: string;
  signalId?: string | null;
  /** Optional reading. Older packs omit these. See ADR-001. */
  value?: number | null;
  unit?: string | null;
  specLow?: number | null;
  specHigh?: number | null;
  toolName?: string | null;
  chamberName?: string | null;
};

export type Hypothesis = {
  id: string;
  category: RootCauseCategory;
  cause: string;
  confidence: number;
  evidenceIds: string[];
  counterEvidence: string[];
  actions: string[];
};

export type Investigation = {
  id: string;
  incidentId: string;
  stage: InvestigationStage;
  evidence: EvidenceItem[];
  hypotheses: Hypothesis[];
  escalationReason?: string | null;
  correlationId: string;
};

export type HumanDecisionRequest = {
  kind: HumanDecisionKind;
  comment?: string | null;
  approverId?: string;
};

export type Capa = {
  id: string;
  incidentId: string;
  summary: string;
  approvalTokenId: string;
  actionHash: string;
  stage: CapaStage;
  openedAt: string;
  approvedBy?: string | null;
  approvedAt?: string | null;
};

export type AuditEvent = {
  at: string;
  kind: string;
  summary: string;
  actor?: string | null;
  toolId?: string | null;
  correlationId?: string | null;
  recordId?: string | null;
};

export type CorrelationTrace = {
  correlationId: string;
  events: AuditEvent[];
};

export type AuditLog = {
  entries: AuditEvent[];
  traces: CorrelationTrace[];
};

export type DecisionResult = {
  investigation: Investigation;
  capa?: Capa | null;
};

export type AdvanceCapaRequest = { stage: CapaStage };

export type FaultInjectionRequest = {
  toolId: string;
  chamberId?: string | null;
  parameter: string;
  shape: FaultShape;
  magnitude: number;
  rootCauseCategory: RootCauseCategory;
  confounders?: boolean;
};

export type FaultInjectionAccepted = {
  faultId: string;
  correlationId: string;
};

export type AdminSession = {
  mode: "local" | "azure";
  isAdmin: boolean;
  displayName: string;
};

export type DemoResetResult = {
  seed: number;
  simulatedOrigin: string;
  fleetToolCount: number;
  ticketCount: number;
  investigationCount: number;
  approvalCount: number;
  capaCount: number;
};

export type ScriptedScenario = {
  id: string;
  title: string;
  rootCauseCategory: RootCauseCategory;
  toolIds: string[];
  chamberId: string;
  parameter: string;
  shape: FaultShape;
  summary: string;
};

export type ScenarioTelemetryPoint = {
  toolId: string;
  chamberId: string;
  parameter: string;
  at: string;
  value: number;
};

export type ScenarioRun = {
  scenarioId: string;
  seed: number;
  faultId: string;
  correlationId: string;
  telemetry: ScenarioTelemetryPoint[];
};

export type MetricsSummary = {
  mode: RunMode;
  completionRate?: number | null;
  groundingScore?: number | null;
  categoryAccuracy?: number | null;
  toolFailureCount?: number;
  latencyP95Ms?: number | null;
  costPerInvestigationUsd?: number | null;
  spendTodayUsd: number;
  spendCapUsd: number;
};

export type ToolStatus = "nominal" | "warning" | "alarm";

export type SiteRole = "Front-end wafer fab" | "Strip, clean, and pilot line";

/** Wafers moving through a plant or a bay. Contract Throughput. */
export type Throughput = {
  waferStartsPerHour: number;
  waferOutsPerHour: number;
  wip: number;
  yieldPct: number;
  uptimePct: number;
};

export type SiteBaySummary = {
  id: string;
  name: string;
  toolCount: number;
};

export type SiteSummary = {
  id: string;
  name: string;
  role: SiteRole;
  location: string;
  timezone: string;
  bays: SiteBaySummary[];
  kpis: Throughput;
};

export type PlacedTool = {
  id: string;
  name: string;
  family: ToolFamily;
  model: string;
  status: ToolStatus;
  x: number;
  y: number;
  siteId: string;
};

export type SiteBayDetail = {
  id: string;
  name: string;
  tools: PlacedTool[];
  kpis: Throughput;
};

export type WaferFlow = {
  fromBayId: string;
  toBayId: string;
  label: string;
};

export type SiteDetail = {
  id: string;
  name: string;
  role: SiteRole;
  location: string;
  timezone: string;
  bays: SiteBayDetail[];
  flows: WaferFlow[];
  kpis: Throughput;
};

export type SiteParameterStats = {
  siteId: string;
  siteName: string;
  mean: number;
  sigma: number;
  alarmRate: number;
  uptimePct: number;
  yieldPct: number;
  discrepancy: boolean;
  reason: string | null;
};

export type ParameterComparison = {
  parameter: string;
  unit: string;
  sites: SiteParameterStats[];
};

export type FamilyComparison = {
  family: ToolFamily;
  referenceSiteId: string;
  parameters: ParameterComparison[];
};
