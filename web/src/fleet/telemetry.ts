// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company
// Display helpers for live sensor values. Values themselves only come from the API.

export type SensorStatus = "inSpec" | "warning" | "outOfSpec";

function decimals(unit: string): number {
  switch (unit) {
    case "atm":
    case "mm":
      return 3;
    case "Torr":
    case "mm/s":
    case "L/min":
      return 2;
    case "C":
    case "mTorr":
    case "percent":
    case "wafers/hour":
    case "s":
    case "sccm":
      return 1;
    default:
      return 0;
  }
}

export function formatValue(value: number, unit: string): string {
  return value.toFixed(decimals(unit));
}
