// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company
// Parameter display names come from the server catalog (labels/catalog.ts, data/metadata/parameters.yaml).

/** Show the contract unit in words a reader already meets in the glossary. */
export function unitLabel(unit: string): string {
  if (unit === "C") return "°C";
  if (unit === "percent") return "%";
  return unit;
}
