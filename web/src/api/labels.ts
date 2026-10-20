// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company
import type { Severity } from "./types";

export type Tone = "nominal" | "warn" | "alarm" | "ok" | "ai";

export function severityTone(severity: Severity): Tone {
  if (severity === "critical" || severity === "high") return "alarm";
  if (severity === "medium") return "warn";
  return "nominal";
}

export function severityWord(severity: Severity): string {
  if (severity === "critical" || severity === "high") return "ALARM";
  if (severity === "medium" || severity === "low") return "WATCH";
  return severity;
}

/** Operator sentence. The clock is the browser's local time, for example "10:42 PM". */
export function approvalSentence(name: string, at: string): string {
  const date = new Date(at);
  const who = name.trim();
  if (!who || Number.isNaN(date.getTime())) return "Approved.";
  const clock = new Intl.DateTimeFormat("en-US", {
    hour: "numeric",
    minute: "2-digit",
  }).format(date);
  return `Approved by ${who} at ${clock}`;
}

/** Join links and members. America/New_York, 24-hour clock, with an ET label. */
export function formatEastern(iso: string): string {
  const date = new Date(iso);
  if (Number.isNaN(date.getTime())) return iso;
  const parts = new Intl.DateTimeFormat("en-US", {
    timeZone: "America/New_York",
    year: "numeric",
    month: "short",
    day: "numeric",
    hour: "2-digit",
    minute: "2-digit",
    hourCycle: "h23",
  }).formatToParts(date);
  const part = (type: Intl.DateTimeFormatPartTypes) => parts.find((item) => item.type === type)?.value ?? "";
  return `${part("month")} ${part("day")}, ${part("year")}, ${part("hour")}:${part("minute")} ET`;
}

export function formatStamp(iso: string, timeZone?: string): string {
  const date = new Date(iso);
  if (Number.isNaN(date.getTime())) return iso;
  // Records are stored in UTC. Show them in the browser's own zone, with the zone name.
  return new Intl.DateTimeFormat("en-GB", {
    timeZone,
    year: "numeric",
    month: "short",
    day: "2-digit",
    hour: "2-digit",
    minute: "2-digit",
    hourCycle: "h23",
    timeZoneName: "short",
  }).format(date);
}

export function formatUsd(amount: number, digits = 2): string {
  return amount.toLocaleString("en-US", {
    style: "currency",
    currency: "USD",
    minimumFractionDigits: digits,
    maximumFractionDigits: digits,
  });
}

export function formatMillions(amount: number): string {
  const millions = amount / 1_000_000;
  if (Number.isInteger(millions)) return `$${millions.toFixed(0)}M`;
  return `$${millions.toFixed(1)}M`;
}
