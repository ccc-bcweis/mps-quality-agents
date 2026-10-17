// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company
// Plant choice for the console. The plants and their tool prefixes come from sites.yaml through the server catalog.

import { plants, siteForTool } from "../labels";

export { siteForTool };

export const SITE_STORAGE_KEY = "mps.selectedSite";

export const ALL_SITES = { id: "all", name: "All sites" } as const;

/** "All sites" followed by every plant in sites.yaml. */
export function plantOptions(): { id: string; name: string }[] {
  return [ALL_SITES, ...plants()];
}

/** True for "all" and for a plant in the catalog. Before the catalog loads any stored id is kept. */
function known(siteId: string): boolean {
  const list = plants();
  return siteId === ALL_SITES.id || list.length === 0 || list.some((plant) => plant.id === siteId);
}

export function readStoredSite(): string {
  try {
    const stored = localStorage.getItem(SITE_STORAGE_KEY);
    if (stored && known(stored)) return stored;
  } catch {
    // Private mode can block storage. The console then stays on every plant.
  }
  return ALL_SITES.id;
}

export function storeSite(siteId: string): string {
  const value = known(siteId) ? siteId : ALL_SITES.id;
  try {
    localStorage.setItem(SITE_STORAGE_KEY, value);
  } catch {
    // The in-memory choice still applies for this tab.
  }
  return value;
}

export function toolInPlant(plantId: string | null | undefined, toolId: string, siteId?: string | null): boolean {
  if (!plantId || plantId === ALL_SITES.id) return true;
  return siteForTool(toolId, siteId) === plantId.trim().toLowerCase();
}
