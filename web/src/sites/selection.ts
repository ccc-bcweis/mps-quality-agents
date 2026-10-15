// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company
// Plant choice for the console. Prefixes match the API when a tool has no site id.

export const SITE_STORAGE_KEY = "mps.selectedSite";

export const PLANTS = [
  { id: "all", name: "All sites" },
  { id: "northwater", name: "Northwater Fab" },
  { id: "ironwood", name: "Ironwood Fab" },
  { id: "lumen", name: "Lumen Pilot Works" },
] as const;

const KNOWN = new Set<string>(PLANTS.map((plant) => plant.id));

export function readStoredSite(): string {
  try {
    const stored = localStorage.getItem(SITE_STORAGE_KEY);
    if (stored && KNOWN.has(stored)) return stored;
  } catch {
    // Private mode can block storage. The console then stays on every plant.
  }
  return "all";
}

export function storeSite(siteId: string): string {
  const value = KNOWN.has(siteId) ? siteId : "all";
  try {
    localStorage.setItem(SITE_STORAGE_KEY, value);
  } catch {
    // The in-memory choice still applies for this tab.
  }
  return value;
}

/** Plant id for a tool. An explicit site id wins. Otherwise I- is Ironwood and P- is Lumen. */
export function siteForTool(toolId: string, siteId?: string | null): string {
  const explicit = siteId?.trim().toLowerCase();
  if (explicit) return explicit;
  if (toolId.startsWith("I-")) return "ironwood";
  if (toolId.startsWith("P-")) return "lumen";
  return "northwater";
}

export function toolInPlant(plantId: string | null | undefined, toolId: string, siteId?: string | null): boolean {
  if (!plantId || plantId === "all") return true;
  return siteForTool(toolId, siteId) === plantId.trim().toLowerCase();
}
