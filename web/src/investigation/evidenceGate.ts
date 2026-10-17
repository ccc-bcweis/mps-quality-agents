// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company
import type { Investigation } from "../api/types";

/** Gate result. Sufficient only once a recommendation was produced; while triage runs it has not been evaluated. */
export function evidenceGateState(investigation: Investigation): "escalate" | "sufficient" | "pending" | "notEvaluated" {
  if (investigation.escalationReason) return "escalate";
  switch (investigation.stage) {
    case "recommendationReady":
    case "awaitingApproval":
    case "approved":
    case "rejected":
      return "sufficient";
    case "started":
    case "evidenceGathered":
      return "pending";
    default:
      return "notEvaluated";
  }
}
