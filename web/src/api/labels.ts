// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company
import type { CapaStage, IncidentChannel, IncidentStage, InvestigationStage, RootCauseCategory, Severity, ToolFamily } from "./types";

export const FAMILY_PROCESS: Record<ToolFamily, string> = {
  deposition: "thin-film deposition",
  etch: "plasma etch",
  strip: "photoresist strip",
  clean: "single-wafer clean",
};

export const CHANNEL_LABEL: Record<IncidentChannel, string> = {
  iotSpcAlert: "IoT SPC",
  operatorReport: "Operator",
  qmsNonconformance: "QMS NCR",
  supplierQualityNotice: "Supplier notice",
  faultInjector: "Fault injector",
};

export const STAGE_LABEL: Record<IncidentStage, string> = {
  ticketOpened: "Ticket",
  investigating: "Investigation",
  recommendationReady: "Recommendation",
  escalated: "Escalated",
  awaitingApproval: "Approval",
  approved: "Approved",
  rejected: "Rejected",
  closed: "Closed",
  closedByDemoReset: "Closed by demo reset",
};

export const INVESTIGATION_STAGE_LABEL: Record<InvestigationStage, string> = {
  started: "Started",
  evidenceGathered: "Evidence gathered",
  recommendationReady: "Recommendation ready",
  escalated: "Escalated",
  awaitingApproval: "Waiting for approval",
  approved: "Approved",
  rejected: "Rejected",
  failed: "Failed",
  closedByDemoReset: "Closed by demo reset",
};

export const CAPA_STAGE_LABEL: Record<CapaStage, string> = {
  open: "Open",
  containment: "Containment",
  rootCauseConfirmed: "Root cause confirmed",
  correctiveAction: "Corrective action",
  verification: "Verification",
  closed: "Closed",
};

export const CATEGORY_LABEL: Record<RootCauseCategory, string> = {
  engineeringChange: "Engineering change",
  equipmentCalibrationOrAlignment: "Equipment calibration / alignment",
  supplyChainMaterial: "Supply-chain material",
  processDrift: "Process drift",
};

const WE1 = "Western Electric rules, rule 1: one point is more than 3σ from the average";
const WE2 = "Western Electric rules, rule 2: 2 of the last 3 points are more than 2σ out on the same side";
const WE3 = "Western Electric rules, rule 3: 4 of the last 5 points are more than 1σ out on the same side";
const WE4 = "Western Electric rules, rule 4: 8 points in a row on one side of the average";
const NELSON = "Trend rule: 6 points in a row each higher than the last";
const WINDOW = "Reading outside the spec window";

/** Plain words for the rule that fired. Keys cover both the API names and the older camel-case names. */
export const RULE_LABEL: Record<string, string> = {
  westernElectric1: WE1,
  westernElectric2: WE2,
  westernElectric3: WE3,
  westernElectric4: WE4,
  "western-electric-1": WE1,
  "western-electric-2": WE2,
  "western-electric-3": WE3,
  "western-electric-4": WE4,
  nelsonTrend: NELSON,
  "nelson-6-rising": NELSON,
  specWindow: WINDOW,
  window: WINDOW,
  manualMeasurement: "Manual measurement outside the printed spec",
};

export function ruleLabel(rule: string): string {
  return RULE_LABEL[rule] ?? rule;
}

export type Tone = "nominal" | "warn" | "alarm" | "ok" | "ai";

export function severityTone(severity: Severity): Tone {
  if (severity === "critical" || severity === "high") return "alarm";
  if (severity === "medium") return "warn";
  return "nominal";
}

export function severityWord(severity: Severity): string {
  if (severity === "critical" || severity === "high") return "ALARM";
  if (severity === "medium" || severity === "low") return "WATCH";
  return severity;
}

/** Operator sentence. The clock is the browser's local time, for example "10:42 PM". */
export function approvalSentence(name: string, at: string): string {
  const date = new Date(at);
  const who = name.trim();
  if (!who || Number.isNaN(date.getTime())) return "Approved.";
  const clock = new Intl.DateTimeFormat("en-US", {
    hour: "numeric",
    minute: "2-digit",
  }).format(date);
  return `Approved by ${who} at ${clock}`;
}

export function formatStamp(iso: string): string {
  const date = new Date(iso);
  if (Number.isNaN(date.getTime())) return iso;
  return new Intl.DateTimeFormat("en-GB", {
    timeZone: "UTC",
    year: "numeric",
    month: "short",
    day: "2-digit",
    hour: "2-digit",
    minute: "2-digit",
    hourCycle: "h23",
  }).format(date);
}

export function formatUsd(amount: number, digits = 2): string {
  return amount.toLocaleString("en-US", {
    style: "currency",
    currency: "USD",
    minimumFractionDigits: digits,
    maximumFractionDigits: digits,
  });
}

export function formatMillions(amount: number): string {
  const millions = amount / 1_000_000;
  if (Number.isInteger(millions)) return `$${millions.toFixed(0)}M`;
  return `$${millions.toFixed(1)}M`;
}
