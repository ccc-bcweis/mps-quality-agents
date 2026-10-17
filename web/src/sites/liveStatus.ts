// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company
import type { FleetLive, SiteDetail, ToolStatus } from "../api/types";

/** Live tool status is the one source: Sites, Production line tiles and the tool page all read it. */
function siteStatusOf(liveStatus: string | undefined): ToolStatus | undefined {
  if (liveStatus === undefined) return undefined;
  if (liveStatus === "watch") return "warning";
  return liveStatus as ToolStatus;
}

export function withLiveStatus(site: SiteDetail, live: FleetLive | null): SiteDetail {
  if (!live) return site;
  const byId = new Map(live.tools.map((tool) => [tool.toolId, tool.status]));
  return {
    ...site,
    bays: site.bays.map((bay) => ({
      ...bay,
      tools: bay.tools.map((tool) => ({ ...tool, status: siteStatusOf(byId.get(tool.id)) ?? tool.status })),
    })),
  };
}
