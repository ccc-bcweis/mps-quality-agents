// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company
import type { Tool } from "../api/types";
import type { Tone } from "../api/labels";
import type { LifecycleId } from "../lifecycle";
import type { SeriesShape } from "../spc/series";

export type TileParam = { label: string; value: string; tone?: Tone };

export type ChartSpec = {
  parameter: string;
  label: string;
  unit: string;
  mean: number;
  sigma: number;
  shape: SeriesShape;
  endValue?: number;
};

export type ToolStory = {
  status: "nominal" | "watch" | "alarm";
  recipe: string;
  note: string;
  lifecycle: LifecycleId;
  escalated: boolean;
  params: TileParam[];
  spark: number[];
  chart: ChartSpec;
  investigationPath?: string;
  ticketNumber?: string;
};

const nominalSpark = [12, 16, 24, 8, 12, 20, 16, 4, 12, 20, 16, 8];
const watchSpark = [16, 15, 16, 14, 9, 6, 12, 21, 24, 18, 8, 4];
const alarmSpark = [24, 23, 22, 23, 22, 22, 23, 10, 6, 5, 6, 4];

/** Readings used only after a fault is active on that tool. First load stays on `stories`. */
const faultStories: Record<string, ToolStory> = {
  "D-01": {
    status: "alarm",
    recipe: "DEP-SIO-2",
    note: "Chamber pressure stepped up.",
    lifecycle: "alert",
    escalated: false,
    params: [
      { label: "Chamber P", value: "6.52 Torr", tone: "alarm" },
      { label: "Heater", value: "372 °C" },
      { label: "RF", value: "448 W" },
    ],
    spark: alarmSpark,
    chart: { parameter: "chamberPressure", label: "Chamber pressure", unit: "Torr", mean: 4, sigma: 0.4, shape: "step", endValue: 6.52 },
  },
  "E-02": {
    status: "watch",
    recipe: "ETCH-OX-14",
    note: "Western Electric rule 2 on ESC temperature.",
    lifecycle: "alert",
    escalated: false,
    params: [
      { label: "P", value: "44 mTorr" },
      { label: "ESC", value: "53.1 °C osc", tone: "warn" },
      { label: "Src RF", value: "1.5 kW" },
    ],
    spark: watchSpark,
    chart: { parameter: "escTemperature", label: "ESC temperature", unit: "°C", mean: 50, sigma: 1.2, shape: "oscillation", endValue: 53.1 },
  },
};

export const stories: Record<string, ToolStory> = {
  "D-01": {
    status: "nominal",
    recipe: "DEP-SIO-2",
    note: "In window.",
    lifecycle: "alert",
    escalated: false,
    params: [
      { label: "Chamber P", value: "4.10 Torr" },
      { label: "Heater", value: "372 °C" },
      { label: "RF", value: "448 W" },
    ],
    spark: nominalSpark,
    chart: { parameter: "chamberPressure", label: "Chamber pressure", unit: "Torr", mean: 4, sigma: 0.4, shape: "nominal" },
  },
  "D-02": {
    status: "nominal",
    recipe: "DEP-SIO-2",
    note: "In window.",
    lifecycle: "alert",
    escalated: false,
    params: [
      { label: "Chamber P", value: "4.05 Torr" },
      { label: "Heater", value: "381 °C" },
      { label: "RF", value: "510 W" },
    ],
    spark: nominalSpark,
    chart: { parameter: "chamberPressure", label: "Chamber pressure", unit: "Torr", mean: 4, sigma: 0.35, shape: "nominal" },
  },
  "E-01": {
    status: "nominal",
    recipe: "ETCH-OX-14",
    note: "In window. Same gas lot as E-02.",
    lifecycle: "alert",
    escalated: false,
    params: [
      { label: "P", value: "42 mTorr" },
      { label: "ESC", value: "51.2 °C" },
      { label: "Src RF", value: "1.4 kW" },
    ],
    spark: nominalSpark,
    chart: { parameter: "escTemperature", label: "ESC temperature", unit: "°C", mean: 50, sigma: 1.1, shape: "nominal" },
  },
  "E-02": {
    status: "nominal",
    recipe: "ETCH-OX-14",
    note: "In window.",
    lifecycle: "alert",
    escalated: false,
    params: [
      { label: "P", value: "44 mTorr" },
      { label: "ESC", value: "51.0 °C" },
      { label: "Src RF", value: "1.5 kW" },
    ],
    spark: nominalSpark,
    chart: { parameter: "escTemperature", label: "ESC temperature", unit: "°C", mean: 50, sigma: 1.2, shape: "nominal" },
  },
  "S-01": {
    status: "nominal",
    recipe: "ASH-O2-7",
    note: "In window.",
    lifecycle: "alert",
    escalated: false,
    params: [
      { label: "P", value: "1.12 Torr" },
      { label: "Temp", value: "262 °C" },
      { label: "Remote RF", value: "3.1 kW" },
    ],
    spark: nominalSpark,
    chart: { parameter: "temperature", label: "Temperature", unit: "°C", mean: 265, sigma: 3, shape: "nominal" },
  },
  "S-02": {
    status: "nominal",
    recipe: "ASH-O2-7",
    note: "In window.",
    lifecycle: "alert",
    escalated: false,
    params: [
      { label: "P", value: "1.08 Torr" },
      { label: "Temp", value: "258 °C" },
      { label: "Remote RF", value: "2.9 kW" },
    ],
    spark: nominalSpark,
    chart: { parameter: "temperature", label: "Temperature", unit: "°C", mean: 265, sigma: 3, shape: "nominal" },
  },
  "C-01": {
    status: "nominal",
    recipe: "CLN-SC1-3",
    note: "In window.",
    lifecycle: "alert",
    escalated: false,
    params: [
      { label: "Bath", value: "64 °C" },
      { label: "Megasonic", value: "720 W" },
      { label: "Adders >45 nm", value: "2" },
    ],
    spark: nominalSpark,
    chart: { parameter: "bathTemperature", label: "Bath temperature", unit: "°C", mean: 65, sigma: 1, shape: "nominal" },
  },
  "C-02": {
    status: "nominal",
    recipe: "CLN-SC1-3",
    note: "In window.",
    lifecycle: "alert",
    escalated: false,
    params: [
      { label: "Bath", value: "66 °C" },
      { label: "Megasonic", value: "680 W" },
      { label: "Adders >45 nm", value: "1" },
    ],
    spark: nominalSpark,
    chart: { parameter: "bathTemperature", label: "Bath temperature", unit: "°C", mean: 65, sigma: 1, shape: "nominal" },
  },
};

export function storyFor(tool: Tool, alerts: { toolId: string; status?: string }[] = []): ToolStory {
  const base = stories[tool.id] ?? {
    status: "nominal" as const,
    recipe: "—",
    note: "No local snapshot. The tile is the API record.",
    lifecycle: "alert" as const,
    escalated: false,
    params: [{ label: "Chambers", value: String(tool.chambers.length) }],
    spark: nominalSpark,
    chart: { parameter: "value", label: "Signal", unit: "", mean: 0, sigma: 1, shape: "nominal" as const },
  };
  // Only an active alert puts the tool in alarm; a cleared alert is history, the same rule as the tool status.
  if (!alerts.some((alert) => alert.toolId === tool.id && alert.status !== "cleared")) return base;
  return (
    faultStories[tool.id] ?? {
      ...base,
      status: "alarm",
      note: "A fault is active on this tool.",
      lifecycle: "alert",
      escalated: false,
    }
  );
}

/** One Operating windows row, built only from live readings and the recipe-step window the server sends. */
export type WindowRow = {
  parameter: string;
  label: string;
  /** Recipe spec for the running step, e.g. "46.0–54.0 °C". */
  window: string;
  /** Control limits: centre ± 3σ. */
  control: string;
  now: string;
  tone: Tone | "muted";
  state: string;
};

const FAMILY_ORDER = ["deposition", "etch", "strip", "clean"];

/** "Deposition, etch, strip, clean": the families in a bay, in process order. The floor plans print the same caption. */
export function bayCaption(families: string[]): string {
  const present = FAMILY_ORDER.filter((family) => families.includes(family));
  const text = present.join(", ");
  return text.charAt(0).toUpperCase() + text.slice(1);
}

export function statusTone(status: ToolStory["status"]): Tone {
  if (status === "alarm") return "alarm";
  if (status === "watch") return "warn";
  return "nominal";
}
