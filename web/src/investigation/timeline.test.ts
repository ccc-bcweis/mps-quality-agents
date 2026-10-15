// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company
import { describe, expect, it } from "vitest";
import { investigations } from "../mocks/demoData";
import { buildAgentSteps, INVESTIGATION_POLL_MS } from "./timeline";

describe("agent step timeline", () => {
  it("keeps the poll slower than a page render and covers every stage chip", () => {
    expect(INVESTIGATION_POLL_MS).toBeGreaterThanOrEqual(5000);
    const s1 = buildAgentSteps(investigations[0]);
    expect(s1.map((step) => step.status)).toEqual([
      "done",
      "done",
      "done",
      "done",
      "done",
      "done",
      "done",
      "done",
      "done",
      "queued",
      "queued",
    ]);
    expect(s1.find((step) => step.id === "hypotheses")?.detail).toMatch(/process drift/);
    expect(s1.find((step) => step.id === "toolCalls")?.payload).toContain("TEL-E02-ESC");
    expect(s1.find((step) => step.id === "ranking")?.prompt).toMatch(/resolution agent/);
    expect(s1.find((step) => step.id === "ticket")?.prompt).toBeNull();
    expect(s1.find((step) => step.id === "toolCalls")?.seconds).toBe(12);
    expect(s1.some((step) => step.status === "running")).toBe(false);

    const s2 = buildAgentSteps(investigations[1]);
    expect(s2.find((step) => step.id === "gate")?.status).toBe("escalated");
    expect(s2.find((step) => step.id === "ranking")?.status).toBe("queued");
    expect(s2.find((step) => step.id === "capaDraft")?.status).toBe("queued");
    expect(s2.find((step) => step.id === "grounding")?.status).toBe("queued");
    expect(s2.find((step) => step.id === "approval")?.status).toBe("queued");

    const started = buildAgentSteps({ ...investigations[0], stage: "started" });
    expect(started.find((step) => step.id === "triage")?.status).toBe("running");
  });
});
