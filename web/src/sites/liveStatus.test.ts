// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company
import { describe, expect, it } from "vitest";
import type { FleetLive, SiteDetail } from "../api/types";
import { toolTile } from "../live/overlay";
import { statusWord } from "./floorLayout";
import { withLiveStatus } from "./liveStatus";

const site = {
  id: "ironwood",
  bays: [{ id: "Etch", tools: [{ id: "I-E-01", status: "warning" }, { id: "I-E-02", status: "nominal" }, { id: "I-E-03", status: "nominal" }] }],
} as unknown as SiteDetail;

const live = {
  at: "2026-10-15T00:00:00Z",
  tools: [
    { toolId: "I-E-01", siteId: "ironwood", status: "nominal", readings: [{ parameter: "p", value: 1, unit: "", status: "nominal" }] },
    { toolId: "I-E-02", siteId: "ironwood", status: "watch", readings: [{ parameter: "p", value: 1, unit: "", status: "watch" }] },
    { toolId: "I-E-03", siteId: "ironwood", status: "alarm", readings: [{ parameter: "p", value: 1, unit: "", status: "alarm" }] },
  ],
} as unknown as FleetLive;

describe("one tool status for Sites, Production line and tool page", () => {
  it("all three views show the same word for every tool", () => {
    const sites = withLiveStatus(site, live).bays[0].tools;
    const words: Record<string, string> = { nominal: "Nominal", watch: "Watch", alarm: "Alarm" };
    for (const row of live.tools) {
      const onSites = statusWord(sites.find((tool) => tool.id === row.toolId)!.status);
      // Production line tiles and the tool page both render toolTile(...).status.
      const story = toolTile(row.toolId, [], row as never);
      expect(onSites).toBe(words[row.status]);
      expect(words[story.status]).toBe(onSites);
    }
  });
});
