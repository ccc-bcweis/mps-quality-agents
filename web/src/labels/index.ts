// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company
// The one place the console names stages, categories, channels and families. Every other module imports from here.
// Parameter names, plants, rule sentences and rule numbers come from the server catalog (./catalog).

import type { CapaStage, IncidentChannel, IncidentStage, InvestigationStage, RootCauseCategory, ToolFamily } from "../api/types";

export * from "./catalog";

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
  ticketOpened: "Ticket opened",
  investigating: "Investigating",
  recommendationReady: "Recommendation ready",
  escalated: "Waiting for more evidence",
  awaitingApproval: "Waiting for approval",
  approved: "Approved",
  rejected: "Rejected",
  closed: "Closed",
  closedByDemoReset: "Closed by reset",
};

export const INVESTIGATION_STAGE_LABEL: Record<InvestigationStage, string> = {
  started: "Started",
  evidenceGathered: "Evidence gathered",
  recommendationReady: "Recommendation ready",
  escalated: "Waiting for more evidence",
  awaitingApproval: "Waiting for approval",
  approved: "Approved",
  rejected: "Rejected",
  failed: "Failed",
  closedByDemoReset: "Closed by reset",
  superseded: "Closed: more evidence added",
};

/** CAPA stages in the order a CAPA moves through them. */
export const CAPA_STAGES: { id: CapaStage; label: string }[] = [
  { id: "open", label: "Open" },
  { id: "containment", label: "Containment" },
  { id: "rootCauseConfirmed", label: "Root cause confirmed" },
  { id: "correctiveAction", label: "Corrective action" },
  { id: "verification", label: "Verification" },
  { id: "closed", label: "Closed" },
];

export function capaStageLabel(stage: CapaStage): string {
  return CAPA_STAGES.find((item) => item.id === stage)?.label ?? stage;
}

export const CATEGORY_LABEL: Record<RootCauseCategory, string> = {
  engineeringChange: "Engineering change",
  equipmentCalibrationOrAlignment: "Equipment (calibration, alignment or wear)",
  supplyChainMaterial: "Supply-chain material",
  processDrift: "Process drift",
};

/** Root-cause categories in display order, with their labels. */
export const CATEGORIES: { id: RootCauseCategory; label: string }[] = (Object.keys(CATEGORY_LABEL) as RootCauseCategory[]).map((id) => ({
  id,
  label: CATEGORY_LABEL[id],
}));
