// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company
import { describe, expect, it } from "vitest";
import {
  approvalReady,
  lifecycleForIncident,
  lifecycleForInvestigation,
  nextCapaStage,
  rejectReady,
} from "./lifecycle";

describe("lifecycle bar mapping", () => {
  it("marks the ticket stage after intake", () => {
    expect(lifecycleForIncident("ticketOpened")).toEqual({ current: "ticket", escalated: false });
  });

  it("keeps an escalation on the investigation step", () => {
    expect(lifecycleForIncident("escalated").escalated).toBe(true);
    expect(lifecycleForInvestigation("escalated").current).toBe("investigation");
  });

  it("moves an approved investigation onto CAPA", () => {
    expect(lifecycleForInvestigation("approved").current).toBe("capa");
    expect(lifecycleForInvestigation("recommendationReady").current).toBe("recommendation");
  });

  it("advances CAPA one legal stage at a time", () => {
    expect(nextCapaStage("open")).toBe("containment");
    expect(nextCapaStage("verification")).toBe("closed");
    expect(nextCapaStage("closed")).toBeNull();
  });
});

describe("approval confirm", () => {
  it("requires the word APPROVE and an approver id", () => {
    expect(approvalReady("APPROVE", "dev-user")).toBe(true);
    expect(approvalReady("approve", "dev-user")).toBe(false);
    expect(approvalReady("APPROVE", "  ")).toBe(false);
  });

  it("requires a reject reason", () => {
    expect(rejectReady("")).toBe(false);
    expect(rejectReady("  ")).toBe(false);
    expect(rejectReady("Need a thermal map.")).toBe(true);
  });
});
