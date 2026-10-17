// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company
import { describe, expect, it } from "vitest";
import type { Investigation, InvestigationStage } from "../api/types";
import { evidenceGateState } from "./evidenceGate";

function at(stage: InvestigationStage, escalationReason?: string): Investigation {
  return { id: "i", incidentId: "n", stage, evidence: [], escalationReason } as unknown as Investigation;
}

describe("evidence gate", () => {
  it("is pending while triage gathers evidence, not sufficient", () => {
    expect(evidenceGateState(at("started"))).toBe("pending");
    expect(evidenceGateState(at("evidenceGathered"))).toBe("pending");
  });

  it("is sufficient only once a recommendation exists", () => {
    for (const stage of ["recommendationReady", "awaitingApproval", "approved", "rejected"] as const) {
      expect(evidenceGateState(at(stage))).toBe("sufficient");
    }
  });

  it("is not evaluated when the run failed or was reset, and escalates with a reason", () => {
    expect(evidenceGateState(at("failed"))).toBe("notEvaluated");
    expect(evidenceGateState(at("closedByDemoReset"))).toBe("notEvaluated");
    expect(evidenceGateState(at("escalated", "Evidence does not tie the lot to the onset."))).toBe("escalate");
  });
});
