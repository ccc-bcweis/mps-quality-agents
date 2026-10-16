// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company
import type { Tool, ToolFamily } from "../api/types";
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
    note: "Chamber pressure stepped outside the 2–6 Torr window.",
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
    note: "Western Electric rule 2 on ESC temperature, still inside 40–60 °C.",
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

export type WindowRow = {
  parameter: string;
  label: string;
  window: string;
  now: string;
  tone: Tone | "muted";
};

const WINDOWS: Record<ToolFamily, { parameter: string; label: string; window: string }[]> = {
  deposition: [
    { parameter: "chamberPressure", label: "Chamber pressure", window: "2–6 Torr" },
    { parameter: "heaterTemperature", label: "Heater", window: "350–400 °C" },
    { parameter: "rfPower", label: "RF power", window: "300–600 W" },
    { parameter: "particles", label: "Particles >45 nm", window: "≤10 adders" },
  ],
  etch: [
    { parameter: "chamberPressure", label: "Chamber pressure", window: "20–80 mTorr" },
    { parameter: "escTemperature", label: "ESC temperature", window: "40–60 °C" },
    { parameter: "wallTemperature", label: "Wall", window: "60 °C" },
    { parameter: "sourceRf", label: "Source RF", window: "1–2 kW" },
    { parameter: "biasRf", label: "Bias RF", window: "200–500 W" },
    { parameter: "particles", label: "Particles >45 nm", window: "≤15 adders" },
  ],
  strip: [
    { parameter: "chamberPressure", label: "Chamber pressure", window: "0.8–1.5 Torr" },
    { parameter: "temperature", label: "Temperature", window: "250–280 °C" },
    { parameter: "remoteRf", label: "Remote plasma", window: "2–4 kW" },
    { parameter: "particles", label: "Particles >45 nm", window: "≤10 adders" },
  ],
  clean: [
    { parameter: "bathTemperature", label: "Chemical bath", window: "60–70 °C" },
    { parameter: "megasonic", label: "Megasonic", window: "500–900 W" },
    { parameter: "particles", label: "Particles >45 nm", window: "≤5 adders" },
  ],
};

const NOW: Record<string, Record<string, { value: string; tone: Tone }>> = {
  "D-01": {
    chamberPressure: { value: "4.10 Torr", tone: "nominal" },
    heaterTemperature: { value: "372 °C", tone: "nominal" },
    rfPower: { value: "448 W", tone: "nominal" },
    particles: { value: "4", tone: "nominal" },
  },
  "E-02": {
    chamberPressure: { value: "44 mTorr", tone: "nominal" },
    escTemperature: { value: "51.0 °C", tone: "nominal" },
    wallTemperature: { value: "60.2 °C", tone: "nominal" },
    sourceRf: { value: "1.50 kW", tone: "nominal" },
    biasRf: { value: "320 W", tone: "nominal" },
    particles: { value: "6", tone: "nominal" },
  },
};

export function windowRows(tool: Tool): WindowRow[] {
  const readings = NOW[tool.id] ?? {};
  return WINDOWS[tool.family].map((row) => {
    const reading = readings[row.parameter];
    return {
      parameter: row.parameter,
      label: row.label,
      window: row.window,
      now: reading?.value ?? "—",
      tone: reading?.tone ?? "muted",
    };
  });
}

export function bayCaption(bay: string): string {
  if (bay === "Bay A") return "deposition / etch";
  if (bay === "Bay B") return "strip / clean";
  return bay;
}

export function statusTone(status: ToolStory["status"]): Tone {
  if (status === "alarm") return "alarm";
  if (status === "watch") return "warn";
  return "nominal";
}
