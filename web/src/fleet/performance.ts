// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

/** Illustrative demo readings. These are not a live tool counter and are not an API resource. */
export type ToolPerformance = {
  toolId: string;
  /** Wafers finished per hour. */
  runRate: number;
  /** Percent of scheduled time the tool was ready. */
  uptime: number;
  /** Overall equipment effectiveness, percent. */
  oee: number;
  /** First-pass yield, percent. */
  firstPassYield: number;
  mtbfHours: number;
  mttrHours: number;
};

export const PERFORMANCE_CAPTION = "Illustrative readings, not a live tool counter.";

export const TOOL_PERFORMANCE: readonly ToolPerformance[] = [
  { toolId: "D-01", runRate: 22.0, uptime: 88, oee: 71, firstPassYield: 94.2, mtbfHours: 140, mttrHours: 4.6 },
  { toolId: "D-02", runRate: 31.0, uptime: 96, oee: 82, firstPassYield: 97.5, mtbfHours: 240, mttrHours: 2.1 },
  { toolId: "E-01", runRate: 27.5, uptime: 95, oee: 80, firstPassYield: 97.1, mtbfHours: 220, mttrHours: 2.4 },
  { toolId: "E-02", runRate: 24.0, uptime: 91, oee: 74, firstPassYield: 95.4, mtbfHours: 160, mttrHours: 3.8 },
  { toolId: "S-01", runRate: 36.0, uptime: 97, oee: 84, firstPassYield: 98.0, mtbfHours: 260, mttrHours: 1.8 },
  { toolId: "S-02", runRate: 34.0, uptime: 96.5, oee: 83, firstPassYield: 97.8, mtbfHours: 250, mttrHours: 2.0 },
  { toolId: "C-01", runRate: 18.0, uptime: 94, oee: 76, firstPassYield: 96.6, mtbfHours: 200, mttrHours: 3.0 },
  { toolId: "C-02", runRate: 19.0, uptime: 94.5, oee: 77, firstPassYield: 96.9, mtbfHours: 210, mttrHours: 2.7 },
];

type Totals = Omit<ToolPerformance, "toolId">;

const ZERO: Totals = {
  runRate: 0,
  uptime: 0,
  oee: 0,
  firstPassYield: 0,
  mtbfHours: 0,
  mttrHours: 0,
};

export function performanceFor(toolId: string): ToolPerformance | undefined {
  return TOOL_PERFORMANCE.find((row) => row.toolId === toolId);
}

/** Mean of the given rows. The fleet figure is this average, not a second copy of the numbers. */
export function rollup(rows: readonly ToolPerformance[] = TOOL_PERFORMANCE): ToolPerformance {
  if (rows.length === 0) return { toolId: "fleet", ...ZERO };
  const total = rows.reduce<Totals>(
    (sum, row) => ({
      runRate: sum.runRate + row.runRate,
      uptime: sum.uptime + row.uptime,
      oee: sum.oee + row.oee,
      firstPassYield: sum.firstPassYield + row.firstPassYield,
      mtbfHours: sum.mtbfHours + row.mtbfHours,
      mttrHours: sum.mttrHours + row.mttrHours,
    }),
    ZERO,
  );
  const count = rows.length;
  return {
    toolId: "fleet",
    runRate: total.runRate / count,
    uptime: total.uptime / count,
    oee: total.oee / count,
    firstPassYield: total.firstPassYield / count,
    mtbfHours: total.mtbfHours / count,
    mttrHours: total.mttrHours / count,
  };
}

function fixed(value: number, digits: number): string {
  const factor = 10 ** digits;
  return (Math.round((value + Number.EPSILON) * factor) / factor).toFixed(digits);
}

export function formatRate(value: number): string {
  return `${fixed(value, 1)} wafers/h`;
}

export function formatPercent(value: number): string {
  return `${fixed(value, 1)}%`;
}

export function formatHours(value: number, digits: number): string {
  return `${fixed(value, digits)} h`;
}
