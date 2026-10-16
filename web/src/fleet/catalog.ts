// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company
import type { Tool } from "../api/types";
import type { Tone } from "../api/labels";
import type { LifecycleId } from "../lifecycle";
import type { SeriesShape } from "../spc/series";
import { offlineFloor } from "virtual:offline-floor";

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

/** Offline teaching-floor tiles from data/metadata/offline-floor.json. Used only without a live API; live tiles come from readings. */
const nominalSpark: number[] = offlineFloor.nominalSpark;

/** Readings used only after a fault is active on that tool. First load stays on `stories`. */
const faultStories = offlineFloor.faultStories as unknown as Record<string, ToolStory>;

export const stories = offlineFloor.stories as unknown as Record<string, ToolStory>;

export function storyFor(tool: Tool, alerts: { toolId: string; status?: string }[] = []): ToolStory {
  const base = stories[tool.id] ?? {
    status: "nominal" as const,
    recipe: "—",
    note: "No local snapshot. The tile is the API record.",
    lifecycle: "alert" as const,
    escalated: false,
    params: [{ label: "Chambers", value: String(tool.chambers.length) }],
    spark: nominalSpark,
    chart: offlineFloor.fallbackChart as ChartSpec,
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
