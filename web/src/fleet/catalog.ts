// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company
import type { Tone } from "../api/labels";

export type TileParam = { label: string; value: string; tone?: Tone };

export type ToolStatus = "nominal" | "watch" | "alarm";

/** A tool tile, built only from live readings and the alert log. No reading yet means no values on the tile. */
export type ToolTile = {
  status: ToolStatus;
  params: TileParam[];
  spark: number[];
};

/** Status before the first live reading: an active alert puts the tool in alarm; a cleared alert is history. */
export function statusFromAlerts(toolId: string, alerts: { toolId: string; status?: string }[] = []): ToolStatus {
  return alerts.some((alert) => alert.toolId === toolId && alert.status !== "cleared") ? "alarm" : "nominal";
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

export function statusTone(status: ToolStatus): Tone {
  if (status === "alarm") return "alarm";
  if (status === "watch") return "warn";
  return "nominal";
}

/** "Waiting for readings…": shown wherever a live value has not arrived yet. */
export const WAITING_FOR_READINGS = "Waiting for readings…";
