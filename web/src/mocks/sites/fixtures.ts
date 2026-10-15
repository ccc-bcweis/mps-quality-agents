// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company
// Plant snapshots for local mode. Generated from Mps:Demo:Seed 4242, the same seed the API uses.

import type { FamilyComparison, SiteDetail, SiteSummary, Tool, ToolFamily } from "../../api/types";
import comparisonsJson from "./comparisons.json";
import detailsJson from "./details.json";

const FAMILIES: readonly ToolFamily[] = ["deposition", "etch", "strip", "clean"];

export const siteDetails = detailsJson as SiteDetail[];

const comparisons = comparisonsJson as Record<ToolFamily, FamilyComparison>;

export function siteSummaries(): SiteSummary[] {
  return siteDetails.map((site) => ({
    id: site.id,
    name: site.name,
    role: site.role,
    location: site.location,
    timezone: site.timezone,
    bays: site.bays.map((bay) => ({ id: bay.id, name: bay.name, toolCount: bay.tools.length })),
    kpis: site.kpis,
  }));
}

export function siteDetail(siteId: string): SiteDetail | undefined {
  return siteDetails.find((site) => site.id === siteId);
}

export function familyComparison(family: string): FamilyComparison | undefined {
  if (!FAMILIES.includes(family as ToolFamily)) return undefined;
  return comparisons[family as ToolFamily];
}

/** A placed tool that is not one of the eight teaching-floor tools. */
export function toolRecord(toolId: string): Tool | undefined {
  for (const site of siteDetails) {
    for (const bay of site.bays) {
      const placed = bay.tools.find((tool) => tool.id === toolId);
      if (!placed) continue;
      return {
        id: placed.id,
        name: placed.name,
        family: placed.family,
        model: placed.model,
        bay: bay.id,
        siteId: site.id,
        chambers: [{ id: `${placed.id}-A`, name: "Chamber A" }],
      };
    }
  }
  return undefined;
}
