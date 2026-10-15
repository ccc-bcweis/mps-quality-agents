// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company
import { describe, expect, it } from "vitest";
import type { TraceNode } from "../api/types";
import { traceLines, valueVsLimit } from "./tracePath";

function node(partial: Partial<TraceNode> & Pick<TraceNode, "eventId" | "type" | "summary" | "occurredAt">): TraceNode {
  return {
    toolId: null,
    toolName: null,
    value: null,
    limitLow: null,
    limitHigh: null,
    children: [],
    truncated: false,
    ...partial,
  };
}

describe("trace path", () => {
  it("collapses streamed readings and keeps the alert spine", () => {
    const readings = [0, 1, 2].map((index) =>
      node({
        eventId: `reading-${index}`,
        type: "telemetry.reading",
        summary: `Reading ${index}`,
        occurredAt: `2026-09-01T00:0${index}:00Z`,
        value: 53.1,
        limitLow: 40,
        limitHigh: 60,
        toolName: "Etch 02",
      }),
    );
    const alert = node({
      eventId: "alert-1",
      type: "alert.raised",
      summary: "A process chart alert fired for chuck temperature.",
      occurredAt: "2026-09-01T00:05:00Z",
      children: [
        node({
          eventId: "rule-1",
          type: "rule.matched",
          summary: "The business rule Edge ring temperature fired.",
          occurredAt: "2026-09-01T00:06:00Z",
        }),
      ],
    });
    const lines = traceLines({
      focusEventId: "fault-1",
      rootEventId: "scenario-1",
      ancestors: [
        node({
          eventId: "scenario-1",
          type: "command.scenario.start",
          summary: "Scenario Edge ring revision C on Etch 02 started.",
          occurredAt: "2026-09-01T00:00:00Z",
        }),
      ],
      tree: node({
        eventId: "fault-1",
        type: "command.fault.inject",
        summary: "A presenter injected a fault on Etch 02.",
        occurredAt: "2026-09-01T00:00:01Z",
        toolName: "Etch 02",
        children: [...readings, alert],
      }),
    });

    expect(lines.map((line) => line.summary)).toEqual([
      "Scenario Edge ring revision C on Etch 02 started.",
      "A presenter injected a fault on Etch 02.",
      "Readings streamed in",
      "A process chart alert fired for chuck temperature.",
      "The business rule Edge ring temperature fired.",
    ]);
    expect(lines[2].readingCount).toBe(3);
    expect(valueVsLimit(lines[2])).toBe("Value 53.1, limit 40 to 60");
    expect(lines.some((line) => line.summary.includes("reading-"))).toBe(false);
    expect(lines[1].focus).toBe(true);
  });

  it("follows a single child and orders investigation steps by time", () => {
    const lines = traceLines({
      focusEventId: "step-late",
      rootEventId: "start-1",
      ancestors: [],
      tree: node({
        eventId: "start-1",
        type: "command.investigation.start",
        summary: "An investigation was started.",
        occurredAt: "2026-09-01T00:00:00Z",
        children: [
          node({
            eventId: "step-late",
            type: "investigation.step",
            summary: "The recommendation was recorded.",
            occurredAt: "2026-09-01T00:10:00Z",
          }),
          node({
            eventId: "step-early",
            type: "investigation.step",
            summary: "The investigation started.",
            occurredAt: "2026-09-01T00:01:00Z",
          }),
        ],
      }),
    });

    expect(lines.map((line) => line.summary)).toEqual([
      "An investigation was started.",
      "The investigation started.",
      "The recommendation was recorded.",
    ]);
    expect(lines[2].focus).toBe(true);
  });
});
