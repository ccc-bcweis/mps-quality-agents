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
  closedByDemoReset: "Closed by reset",
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
  closedByDemoReset: "Closed by reset",
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
  equipmentCalibrationOrAlignment: "Equipment (calibration, alignment or wear)",
  supplyChainMaterial: "Supply-chain material",
  processDrift: "Process drift",
};

const WE1 = "Western Electric rules, rule 1: one point is more than 3σ from the average";
const WE2 = "Western Electric rules, rule 2: 2 of the last 3 points are more than 2σ out on the same side";
const WE3 = "Western Electric rules, rule 3: 4 of the last 5 points are more than 1σ out on the same side";
const WE4 = "Western Electric rules, rule 4: 8 points in a row on one side of the average";
const NELSON = "Nelson rule 6: 6 readings in a row, each higher than the last";
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

const WE_NUMBER: Record<string, number> = {
  westernElectric1: 1, westernElectric2: 2, westernElectric3: 3, westernElectric4: 4,
  "western-electric-1": 1, "western-electric-2": 2, "western-electric-3": 3, "western-electric-4": 4,
};
const NELSON_IDS = new Set(["nelsonTrend", "nelson-6-rising"]);
const WINDOW_IDS = new Set(["specWindow", "window"]);

/** "1-4", "1 and 3", "1, 2 and 4": consecutive numbers collapse into a range. */
function numberList(numbers: number[]): string {
  const sorted = [...new Set(numbers)].sort((a, b) => a - b);
  const runs: string[] = [];
  for (let i = 0; i < sorted.length; ) {
    let j = i;
    while (j + 1 < sorted.length && sorted[j + 1] === sorted[j] + 1) j++;
    runs.push(j - i >= 2 ? `${sorted[i]}-${sorted[j]}` : sorted.slice(i, j + 1).join(", "));
    i = j + 1;
  }
  const parts = runs.join(", ").split(", ");
  return parts.length > 1 ? `${parts.slice(0, -1).join(", ")} and ${parts[parts.length - 1]}` : parts[0];
}

/**
 * Plain words for the rule or rules that fired. One rule keeps its full sentence. Several rules
 * (an alert that kept firing, ids joined with "+") read as one phrase, for example
 * "Western Electric rules 1-4 and Nelson rule 6 (rising trend)".
 */
export function ruleLabel(rule: string): string {
  const ids = [...new Set(rule.split("+").map((id) => id.trim()).filter(Boolean))];
  if (ids.length <= 1) return RULE_LABEL[ids[0] ?? rule] ?? rule;
  const we = ids.filter((id) => id in WE_NUMBER).map((id) => WE_NUMBER[id]);
  const phrases: string[] = [];
  if (we.length > 0) phrases.push(`Western Electric ${we.length > 1 ? "rules" : "rule"} ${numberList(we)}`);
  if (ids.some((id) => NELSON_IDS.has(id))) phrases.push("Nelson rule 6 (rising trend)");
  if (ids.some((id) => WINDOW_IDS.has(id))) phrases.push("reading outside the spec window");
  for (const id of ids) {
    if (!(id in WE_NUMBER) && !NELSON_IDS.has(id) && !WINDOW_IDS.has(id)) phrases.push(RULE_LABEL[id] ?? id);
  }
  return phrases.length > 1 ? `${phrases.slice(0, -1).join(", ")} and ${phrases[phrases.length - 1]}` : phrases[0];
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

export function formatStamp(iso: string, timeZone?: string): string {
  const date = new Date(iso);
  if (Number.isNaN(date.getTime())) return iso;
  // Records are stored in UTC. Show them in the browser's own zone, with the zone name.
  return new Intl.DateTimeFormat("en-GB", {
    timeZone,
    year: "numeric",
    month: "short",
    day: "2-digit",
    hour: "2-digit",
    minute: "2-digit",
    hourCycle: "h23",
    timeZoneName: "short",
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
