// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

export function byNewestTime<T>(time: (item: T) => string): (left: T, right: T) => number {
  return (left, right) => {
    const leftMs = Date.parse(time(left));
    const rightMs = Date.parse(time(right));
    return (Number.isNaN(rightMs) ? 0 : rightMs) - (Number.isNaN(leftMs) ? 0 : leftMs);
  };
}

export function toolPlainName(
  tools: readonly { id: string; name: string }[],
  toolId: string | null | undefined,
): string {
  if (!toolId) return "Unknown tool";
  return tools.find((tool) => tool.id === toolId)?.name || toolId;
}
