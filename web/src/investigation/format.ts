// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company
import type { EvidenceItem, EvidenceSource } from "../api/types";
import { parameterLabel, unitLabel, PARAMETER_LABEL } from "../sites/parameters";

export const SOURCE_LABEL: Record<EvidenceSource, string> = {
  telemetry: "Tool sensors",
  spc: "Process chart",
  mes: "Manufacturing system",
  qms: "Quality system",
  plm: "Product records",
  erp: "Materials system",
  cmms: "Maintenance system",
  document: "Service document",
  ticketing: "Tickets",
};

const FAMILY: Record<string, string> = {
  D: "Deposition",
  E: "Etch",
  S: "Strip",
  C: "Clean",
};

const TOOL_ID = /\b([DESC])-(\d{2})\b/g;
const RECORD_ID = /\b(?:ECO|NCR|PM|CAL|TEL|SB|NF3|WO|L)-[A-Z0-9]+(?:-[A-Z0-9]+)*\b/g;
const STAMP = /\d{4}-\d{2}-\d{2}[ T]\d{2}:\d{2}(?::\d{2})?(?:\.\d+)?(?:\s*UTC|Z|[+-]\d{2}:?\d{2})?/g;
const VALUE_UNIT = /(-?\d+(?:\.\d+)?)\s*(°C|mTorr|sccm|mm\/s|kW|Torr|C|W|%)/;
const SPEC_WINDOW = /(\d+(?:\.\d+)?)\s*[–-]\s*(\d+(?:\.\d+)?)\s*(°C|mTorr|sccm|mm\/s|kW|Torr|C|W|%)/;
const GUID = /\b[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}\b/gi;

export function formatLocalWhen(iso: string, timeZone?: string): string {
  const date = new Date(iso);
  if (Number.isNaN(date.getTime())) return "—";
  const parts = new Intl.DateTimeFormat("en-US", {
    timeZone,
    month: "short",
    day: "numeric",
    hour: "numeric",
    minute: "2-digit",
    hour12: true,
  }).formatToParts(date);
  const read = (type: Intl.DateTimeFormatPartTypes) => parts.find((part) => part.type === type)?.value ?? "";
  const month = read("month");
  const day = read("day");
  const hour = read("hour");
  const minute = read("minute");
  const dayPeriod = read("dayPeriod").replace(/\./g, "").toUpperCase();
  if (!month || !day || !hour || !minute || (dayPeriod !== "AM" && dayPeriod !== "PM")) return "—";
  return `${month} ${day}, ${hour}:${minute} ${dayPeriod}`;
}

export function formatMeasure(value: number): string {
  if (!Number.isFinite(value)) return "";
  const text = value.toFixed(3).replace(/\.?0+$/, "");
  return text === "-0" ? "0" : text;
}

export function plainTool(toolId?: string | null): string | null {
  if (!toolId) return null;
  const match = /^([DESC])-(\d{2})$/.exec(toolId.trim());
  if (!match) return null;
  const family = FAMILY[match[1] ?? ""];
  if (!family) return null;
  return `${family} tool ${match[1]}-${match[2]}`;
}

export function plainChamber(chamberId?: string | null): string | null {
  if (!chamberId) return null;
  const id = chamberId.trim();
  const named = /^chamber\s+([A-D])$/i.exec(id);
  if (named?.[1]) return `Chamber ${named[1].toUpperCase()}`;
  if (/^[A-D]$/.test(id)) return `Chamber ${id}`;
  const suffixed = /-([A-D])$/.exec(id);
  if (suffixed?.[1]) return `Chamber ${suffixed[1]}`;
  const indexed = /^C([1-4])$/.exec(id);
  if (indexed?.[1]) return `Chamber ${"ABCD"[Number(indexed[1]) - 1]}`;
  return null;
}

export function placeLabel(
  toolId?: string | null,
  chamberId?: string | null,
  toolName?: string | null,
  chamberName?: string | null,
): string {
  const tool = toolName?.trim() || plainTool(toolId);
  const chamber = chamberName?.trim() || plainChamber(chamberId);
  if (tool && chamber && !tool.includes(chamber)) return `${tool}, ${chamber}`;
  return tool || chamber || "—";
}

/** Drop record ids and inline timestamps from a sentence the operator reads. */
export function plainNarrative(text: string): string {
  return text
    .replace(GUID, "")
    .replace(STAMP, "")
    .replace(TOOL_ID, (_match, letter: string, num: string) => `${FAMILY[letter] ?? "Tool"} tool ${letter}-${num}`)
    .replace(RECORD_ID, "")
    .replace(/\s{2,}/g, " ")
    .replace(/\s+([.,;:])/g, "$1")
    .trim();
}

export function oneSentence(text: string): string {
  const cleaned = plainNarrative(text);
  const sentence = cleaned.split(/(?<=[.!?])\s/)[0] ?? cleaned;
  if (sentence.length <= 160) return sentence;
  return `${sentence.slice(0, 157).trimEnd()}…`;
}

export function parameterName(item: { title: string; excerpt: string; signalId?: string | null }): string | null {
  const compact = `${item.signalId ?? ""} ${item.title} ${item.excerpt}`.toLowerCase().replace(/[^a-z0-9]/g, "");
  const keys = Object.keys(PARAMETER_LABEL).sort((left, right) => right.length - left.length);
  for (const key of keys) {
    if (compact.includes(key.toLowerCase())) return parameterLabel(key);
  }
  return null;
}

export function whatItShows(item: EvidenceItem): string {
  const parameter = parameterName(item);
  const place = placeLabel(item.toolId, null, item.toolName, item.chamberName);
  if (parameter && place !== "—") return `${parameter} on ${place}`;
  return oneSentence(item.excerpt || item.title) || "Recorded evidence";
}

export function valueVsSpec(item: Pick<EvidenceItem, "value" | "unit" | "specLow" | "specHigh" | "excerpt">): string {
  const parsed = parseReading(item.excerpt);
  const value = item.value ?? parsed.value;
  if (value == null || !Number.isFinite(value)) return "—";
  const unit = unitLabel(item.unit ?? parsed.unit ?? "");
  const low = item.specLow ?? parsed.low;
  const high = item.specHigh ?? parsed.high;
  const valueText = unit ? `${formatMeasure(value)} ${unit}` : formatMeasure(value);
  if (low == null || high == null) return valueText;
  const spec = `${formatMeasure(low)}-${formatMeasure(high)}`;
  return unit ? `${valueText}, spec ${spec} ${unit}` : `${valueText}, spec ${spec}`;
}

export function readableExcerpt(text: string): string {
  return text.replace(STAMP, (stamp) => {
    const iso = stamp.includes("T") ? stamp.replace(/\s*UTC$/, "Z") : `${stamp.replace(" UTC", "Z").replace(" ", "T")}`;
    const local = formatLocalWhen(iso);
    return local === "—" ? stamp : local;
  });
}

export function formatStepDuration(status: "queued" | "running" | "done" | "escalated", seconds: number): string {
  if (status === "queued") return "Not started";
  if (status === "running") return "Running";
  const whole = Math.max(1, Math.round(seconds));
  return whole === 1 ? "1 s" : `${whole} s`;
}

function parseReading(excerpt: string): { value?: number; unit?: string; low?: number; high?: number } {
  const spec = SPEC_WINDOW.exec(excerpt);
  const value = VALUE_UNIT.exec(excerpt);
  return {
    value: value ? Number(value[1]) : undefined,
    unit: value?.[2] ?? spec?.[3],
    low: spec ? Number(spec[1]) : undefined,
    high: spec ? Number(spec[2]) : undefined,
  };
}
