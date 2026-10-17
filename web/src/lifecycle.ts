// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company
import type { CapaStage, IncidentStage, InvestigationStage } from "./api/types";
import { CAPA_STAGES } from "./labels";

export const LIFECYCLE = [
  { id: "alert", label: "Alert" },
  { id: "ticket", label: "Ticket" },
  { id: "investigation", label: "Investigation" },
  { id: "recommendation", label: "Recommendation" },
  { id: "approval", label: "Approval" },
  { id: "capa", label: "CAPA" },
] as const;

export type LifecycleId = (typeof LIFECYCLE)[number]["id"];

export type LifecycleView = {
  current: LifecycleId;
  escalated: boolean;
};


export function lifecycleForIncident(stage: IncidentStage): LifecycleView {
  switch (stage) {
    case "ticketOpened":
      return { current: "ticket", escalated: false };
    case "investigating":
      return { current: "investigation", escalated: false };
    case "recommendationReady":
      return { current: "recommendation", escalated: false };
    case "escalated":
      return { current: "investigation", escalated: true };
    case "awaitingApproval":
      return { current: "approval", escalated: false };
    case "approved":
    case "closed":
    case "closedByDemoReset":
      return { current: "capa", escalated: false };
    case "rejected":
      return { current: "approval", escalated: false };
    default:
      return { current: "alert", escalated: false };
  }
}

export function lifecycleForInvestigation(stage: InvestigationStage): LifecycleView {
  switch (stage) {
    case "started":
    case "evidenceGathered":
    case "failed":
      return { current: "investigation", escalated: false };
    case "recommendationReady":
      return { current: "recommendation", escalated: false };
    case "escalated":
      return { current: "investigation", escalated: true };
    case "awaitingApproval":
      return { current: "approval", escalated: false };
    case "approved":
      return { current: "capa", escalated: false };
    case "rejected":
      return { current: "approval", escalated: false };
    case "closedByDemoReset":
    case "superseded":
      return { current: "investigation", escalated: false };
    default:
      return { current: "alert", escalated: false };
  }
}

export function isClosedStage(stage: IncidentStage): boolean {
  return stage === "closed" || stage === "closedByDemoReset";
}

export function nextCapaStage(stage: CapaStage): CapaStage | null {
  const index = CAPA_STAGES.findIndex((item) => item.id === stage);
  if (index < 0 || index >= CAPA_STAGES.length - 1) return null;
  return CAPA_STAGES[index + 1].id;
}

export const APPROVE_WORD = "APPROVE";

export function approvalReady(word: string, approverId: string): boolean {
  return word === APPROVE_WORD && approverId.trim().length > 0;
}

export function rejectReady(comment: string): boolean {
  return comment.trim().length > 0;
}
