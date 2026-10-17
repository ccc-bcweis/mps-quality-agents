// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

/** Plant id from the tool prefix. I- is Ironwood, P- is Lumen, and the rest is Northwater. */
function plantOfTool(toolId: string): string {
  if (toolId.startsWith("I-")) return "ironwood";
  if (toolId.startsWith("P-")) return "lumen";
  return "northwater";
}

export function matchesPlant(siteId: string | null, toolId: string | null | undefined): boolean {
  if (!siteId || siteId.trim().length === 0) return true;
  if (!toolId) return false;
  return plantOfTool(toolId).toLowerCase() === siteId.trim().toLowerCase();
}
