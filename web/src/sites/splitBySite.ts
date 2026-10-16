// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company
import { toolInPlant } from "./selection";

/** One list drives both the rows and the banner count. Under All sites nothing is elsewhere. */
export function splitBySite<T>(items: T[], siteId: string, toolFor: (item: T) => string | undefined): { visible: T[]; elsewhere: number } {
  if (siteId === "all") return { visible: items, elsewhere: 0 };
  const visible: T[] = [];
  let elsewhere = 0;
  for (const item of items) {
    const toolId = toolFor(item);
    if (toolId && toolInPlant(siteId, toolId)) visible.push(item);
    else if (toolId) elsewhere += 1;
  }
  return { visible, elsewhere };
}
