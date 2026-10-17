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
  | "closed"
  | "closedByDemoReset";
export type EvidenceSource =
  | "telemetry"
  | "spc"
  | "mes"
  | "qms"
  | "plm"
  | "erp"
  | "cmms"
  | "document"
  | "ticketing"
  | "manualMeasurement";
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
  | "failed"
  | "closedByDemoReset";
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
  /** Active until the signal stays in control for 8 readings in a row. */
  status?: "active" | "cleared";
  clearedAt?: string | null;
  /** Newest violation folded into this active alert. */
  lastSeenAt?: string | null;
  /** Evaluations that found violations while the alert was active. */
  occurrences?: number;
};

export type Ticket = {
  id: string;
  number: string;
  incidentId: string;
  toolId: string;
  title: string;
  openedAt: string;
  /** Open until the ticket's incident is closed (by work or by reset). From the API. */
  status?: "open" | "closed";
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
  statusLabel?: string | null;
};

export type OpenIncidentRequest = {
  toolId: string;
  chamberId?: string | null;
  parameter: string;
  summary: string;
  severity?: Severity;
};

export type PrintoutInspection = {
  toolId: string;
  chamberId?: string | null;
  observedAt: string;
  instrument: string;
  parameter: string;
  value: number;
  unit: string;
  specMinimum: number;
  specMaximum: number;
  shape?: string | null;
  operatorInitials?: string | null;
  parseMethod: "text" | "model";
  outOfSpec: boolean;
  /** The upload the server kept; send it on confirm to attach the sheet as evidence. */
  fileId?: string | null;
};

export type ManualReading = {
  evidenceId: string;
  /** The ticket the rules engine opened for this sample, if any. */
  incidentId?: string | null;
  alertId?: string | null;
  toolId: string;
  siteId: string;
  chamberId?: string | null;
  parameter: string;
  instrument: string;
  value: number;
  unit: string;
  specMinimum: number;
  specMaximum: number;
  outOfSpec: boolean;
  observedAt: string;
  /** The printed time when the user pressed Now. */
  printedObservedAt?: string | null;
  olderThanWindow: boolean;
  operatorInitials?: string | null;
  fileName?: string | null;
  fileUrl?: string | null;
  source: string;
  /** True while the alert this sample raised is still active. A resolved reading is history only. */
  active: boolean;
};

export type PrintoutConfirmRequest = {
  toolId: string;
  chamberId?: string | null;
  observedAt: string;
  instrument: string;
  parameter: string;
  value: number;
  unit: string;
  specMinimum: number;
  specMaximum: number;
  shape?: string | null;
  operatorInitials?: string | null;
  /** The time printed on the sheet, kept for the audit trail when the user pressed Now. */
  printedObservedAt?: string | null;
  fileId?: string | null;
};

export type PrintoutConfirmation = {
  evidenceId: string;
  incidentId?: string | null;
  faultId?: string | null;
  correlationId?: string | null;
  alertId?: string | null;
  appliedToSimulation: boolean;
  outOfSpec: boolean;
  printedParameter: string;
  catalogParameter: string;
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
  sourceEventIds?: string[] | null;
  /** Centre ± 3σ of the control chart the rule was judged against. */
  controlLow?: number | null;
  controlHigh?: number | null;
  center?: number | null;
  limitState?: LimitState | null;
};

/** outOfSpec: outside the recipe spec. outOfControl: inside spec but statistically unusual. */
export type LimitState = "outOfSpec" | "outOfControl" | "inControl";

export type TraceNode = {
  eventId: string;
  type: string;
  summary: string;
  occurredAt: string;
  toolId?: string | null;
  toolName?: string | null;
  value?: number | null;
  limitLow?: number | null;
  limitHigh?: number | null;
  children: TraceNode[];
  truncated: boolean;
};

export type CausalTrace = {
  focusEventId: string;
  rootEventId: string;
  ancestors: TraceNode[];
  tree: TraceNode;
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
  statusLabel?: string | null;
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
  /** Tool the CAPA belongs to, resolved from its incident by the API. */
  toolId?: string | null;
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
  /** Optional. The fault ends this many minutes after onset. */
  durationMinutes?: number;
};

export type FaultInjectionAccepted = {
  faultId: string;
  correlationId: string;
};

export type AdminSession = {
  mode: "local" | "azure";
  isAdmin: boolean;
  displayName: string;
  /** False when signed in but not invited to the hosted demo. */
  member?: boolean;
};

export type JoinLink = {
  id: string;
  url: string;
  createdAt: string;
  expiresAt: string;
  maxUses: number;
  uses: number;
  revoked: boolean;
  status: "active" | "expired" | "usedUp" | "revoked";
};

export type JoinLinks = {
  links: JoinLink[];
  members: { email?: string | null; displayName?: string | null; joinedAt: string }[];
};

export type RedeemResult = { result: "joined" | "alreadyMember"; message: string };

export type DemoResetResult = {
  seed: number;
  simulatedOrigin: string;
  fleetToolCount: number;
  ticketCount: number;
  investigationCount: number;
  approvalCount: number;
  capaCount: number;
  telemetryCount: number;
  eventCount: number;
  resetEventId: string;
  kind: "hard" | "soft";
};

export type ResetKind = "hard" | "soft";

export type ResetProgress = {
  running: boolean;
  kind?: ResetKind | null;
  phase: string;
  message: string;
};

/** Live follows the running plant. Replay shows one recorded scenario. */
export type DataMode = {
  mode: "live" | "replay";
  scenarioId?: string | null;
  messageCount: number;
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
  whatsHappening: string;
  whatWeSimulate: string;
  whatAiShouldFind: string;
  expectedCause: string;
};

export type LiveStatus = "nominal" | "watch" | "alarm";

export type LiveReading = {
  sensorId: string;
  parameter: string;
  chamberId: string;
  unit: string;
  at: string;
  value: number;
  status: LiveStatus;
  /** Recipe-step window from the fleet data; absent for counters. */
  specLow?: number | null;
  specHigh?: number | null;
  center?: number | null;
  controlLow?: number | null;
  controlHigh?: number | null;
  limitState?: LimitState | null;
};

export type LivePoint = { at: string; value: number };

export type LiveSeries = {
  parameter: string;
  chamberId: string;
  points: LivePoint[];
  /** Limits the rules engine checks. Null or absent for cumulative counters. */
  center?: number | null;
  sigma?: number | null;
};

export type FleetLiveTool = {
  toolId: string;
  siteId: string;
  status: LiveStatus;
  readings: LiveReading[];
};

export type FleetLive = {
  simulatedNow: string;
  tools: FleetLiveTool[];
};

export type ToolLive = {
  toolId: string;
  siteId: string;
  simulatedNow: string;
  status: LiveStatus;
  readings: LiveReading[];
  series: LiveSeries[];
  /** Readable name of the running recipe, from the fleet data. */
  recipeName?: string | null;
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

export type CapabilityType =
  | "manufacturing"
  | "warehousing"
  | "researchAndDevelopment"
  | "sales"
  | "backOffice"
  | "other";

export type CompanyMetadata = {
  id: string;
  name: string;
  purpose: string;
  description: string;
  industry: string;
  loadMilliseconds: number;
};

export type MetadataToolCard = {
  id: string;
  name: string;
  toolType: string;
  simulated: boolean;
};

export type MetadataLine = {
  id: string;
  name: string;
  lineType: string;
  sequence: number;
  characteristics: string;
  stepNames: string[];
  tools: MetadataToolCard[];
};

export type MetadataCapability = {
  id: string;
  name: string;
  type: CapabilityType;
  description: string;
  simulated: boolean;
  lines: MetadataLine[];
  directTools: MetadataToolCard[];
};

export type MetadataSite = {
  id: string;
  name: string;
  purpose: string;
  description: string;
  timezone: string;
  capabilities: MetadataCapability[];
};

export type MetadataPort = {
  name: string;
  description: string;
};

export type MetadataParameter = {
  id: string;
  name: string;
  parameterClass: string;
  unit: string;
  lcl: number;
  ucl: number;
  alertLow: number;
  alertHigh: number;
  nominal: number;
  telemetry: boolean;
};

export type MetadataTool = {
  id: string;
  name: string;
  toolType: string;
  siteId: string;
  siteName: string;
  lineId: string | null;
  lineName: string | null;
  capabilityId: string | null;
  simulated: boolean;
  inputs: MetadataPort[];
  outputs: MetadataPort[];
  parameters: MetadataParameter[];
};

export type RuleAction = {
  type: string;
  priority?: string | null;
  queue?: string | null;
};

export type BusinessRule = {
  id: string;
  name: string;
  whenPlain: string;
  thenPlain: string;
  why: string;
  order: number;
  actions: RuleAction[];
  /** The rule's configured conditions (from business-rules.yaml), one field each. */
  conditions?: RuleCondition[];
};

export type RuleCondition = {
  field: string;
  label: string;
  kind: "text" | "number" | "toggle";
  value: string;
};

/** A rule threshold or setting from the loaded configuration. Value is text; toggles are "true" or "false". */
export type RuleSetting = {
  id: string;
  group: string;
  label: string;
  kind: "number" | "toggle";
  value: string;
  unit?: string | null;
  help?: string | null;
};

export type ProcessStep = {
  id: string;
  name: string;
  order: number;
  detail?: string | null;
  highlightStages: IncidentStage[];
};

export type ProcessTransition = {
  from: string;
  to: string;
  name: string;
};

export type ProcessMap = {
  id: string;
  name: string;
  state: "current" | "future";
  steps: ProcessStep[];
  transitions: ProcessTransition[];
};
