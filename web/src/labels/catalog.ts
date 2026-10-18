// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company
// The labels, limits and rule sentences the server reads from data/ (GET /api/metadata/catalog).
// The console keeps no copy: every number, parameter name, plant and rule sentence on screen comes from here.

import { useEffect, useState } from "react";
import type { ConsoleCatalog } from "../api/types";

const EMPTY: ConsoleCatalog = { limits: null, rules: [], parameters: [], sites: [] };

let current: ConsoleCatalog = EMPTY;
let loading: Promise<ConsoleCatalog> | null = null;
let parameterPattern: RegExp | null = null;
const listeners = new Set<() => void>();

/** Replaces the loaded catalog. Tests call this with web/src/test/catalog.fixture.json. */
export function setCatalog(next: ConsoleCatalog): void {
  current = next;
  parameterPattern = null;
  listeners.forEach((listener) => listener());
}

export function catalogLoaded(): boolean {
  return current.limits !== null;
}

/** Loads the catalog once. A failed load (signed out) can be retried; the console shows ids until it lands. */
export function loadCatalog(fetcher: () => Promise<ConsoleCatalog>): Promise<ConsoleCatalog> {
  if (catalogLoaded()) return Promise.resolve(current);
  loading ??= fetcher()
    .then((next) => {
      setCatalog(next);
      return next;
    })
    .finally(() => {
      loading = null;
    });
  return loading;
}

/** Waits between catalog retries, in milliseconds. The last wait repeats until the catalog lands. */
export const CATALOG_RETRY_MS = [1000, 2000, 5000, 10000, 30000];

/**
 * Loads the catalog and keeps retrying with backoff until it lands, so one failed request (an app restart during a
 * deploy, a cold start) cannot leave the console without limits, chart bands, plants and labels until a reload.
 * Returns a stop function for effect cleanup.
 */
export function keepLoadingCatalog(fetcher: () => Promise<ConsoleCatalog>, waits: number[] = CATALOG_RETRY_MS): () => void {
  let stopped = false;
  let timer: ReturnType<typeof setTimeout> | undefined;
  const attempt = (tries: number) => {
    if (stopped || catalogLoaded()) return;
    loadCatalog(fetcher).catch(() => {
      if (stopped || catalogLoaded()) return;
      timer = setTimeout(() => attempt(tries + 1), waits[Math.min(tries, waits.length - 1)]);
    });
  };
  attempt(0);
  return () => {
    stopped = true;
    if (timer !== undefined) clearTimeout(timer);
  };
}

/** Re-renders the caller when the catalog arrives. */
export function useCatalog(): ConsoleCatalog {
  const [, setVersion] = useState(0);
  useEffect(() => {
    const listener = () => setVersion((version) => version + 1);
    listeners.add(listener);
    return () => {
      listeners.delete(listener);
    };
  }, []);
  return current;
}

/** The rule numbers the server runs with. Null until the catalog has loaded. */
export function limits(): ConsoleCatalog["limits"] {
  return current.limits;
}

/** A number for a sentence, or a dash when the catalog has not loaded. */
export function limitText(value: number | undefined | null): string {
  return value === undefined || value === null ? "—" : String(value);
}

/**
 * Plain words for the rule that fired. One rule keeps its full sentence. Several rules (an alert that kept firing,
 * ids joined with "+") read as one phrase, for example "Western Electric rules 1-4 and Nelson rule 6 (rising trend)".
 */
export function ruleLabel(rule: string): string {
  const ids = [...new Set(rule.split("+").map((id) => id.trim()).filter(Boolean))];
  const sentence = (id: string) => current.rules.find((item) => item.id === id)?.label ?? id;
  if (ids.length <= 1) return sentence(ids[0] ?? rule);
  const we = ids.map(westernElectricNumber).filter((n): n is number => n !== null);
  const phrases: string[] = [];
  if (we.length > 0) phrases.push(`Western Electric ${we.length > 1 ? "rules" : "rule"} ${numberList(we)}`);
  if (ids.includes(NELSON_ID)) phrases.push("Nelson rule 6 (rising trend)");
  if (ids.includes(WINDOW_ID)) phrases.push("reading outside the spec window");
  for (const id of ids) {
    if (westernElectricNumber(id) === null && id !== NELSON_ID && id !== WINDOW_ID) phrases.push(sentence(id));
  }
  return phrases.length > 1 ? `${phrases.slice(0, -1).join(", ")} and ${phrases[phrases.length - 1]}` : phrases[0];
}

/** The short form of one rule ("2 of 3 beyond 2 sigma, one side"), or null when the catalog has not loaded. */
export function ruleBrief(rule: string): string | null {
  return current.rules.find((item) => item.id === rule)?.brief ?? null;
}

const NELSON_ID = "nelson-6-rising";
const WINDOW_ID = "window";

function westernElectricNumber(id: string): number | null {
  const match = /^western-electric-(\d+)$/.exec(id);
  return match ? Number(match[1]) : null;
}

/** "1-4", "1 and 3", "1, 2 and 4": consecutive numbers collapse into a range. */
function numberList(numbers: number[]): string {
  const sorted = [...new Set(numbers)].sort((a, b) => a - b);
  const runs: string[] = [];
  for (let i = 0; i < sorted.length; ) {
    let j = i;
    while (j + 1 < sorted.length && sorted[j + 1] === sorted[j] + 1) j++;
    runs.push(j - i >= 2 ? `${sorted[i]}-${sorted[j]}` : sorted.slice(i, j + 1).join(", "));
    i = j + 1;
  }
  const parts = runs.join(", ").split(", ");
  return parts.length > 1 ? `${parts.slice(0, -1).join(", ")} and ${parts[parts.length - 1]}` : parts[0];
}

// Keys are the lower-cased parts of a sensor id.
const SENSOR_WORDS: Record<string, string> = {
  rf: "RF",
  bias: "Bias",
  src: "Source",
  rfl: "reflected",
  fwd: "forward",
  output: "Output",
  throughput: "Throughput",
  run: "Run",
  rate: "rate",
  temp: "temperature",
  press: "pressure",
};

/**
 * A plain label for a sensor id such as E-01-A-RF-BIAS-RFL: "A Bias RF reflected".
 * The tool id is dropped (the tile already names the tool) and the chamber letter leads.
 */
export function sensorIdLabel(sensorId: string): string | null {
  const match = /^(?:[A-Z]-)?[A-Z]-\d{2}(?:-([A-Z]))?-(.+)$/.exec(sensorId);
  if (!match) return null;
  const [, chamber, rest] = match;
  const tokens = rest.toLowerCase().split("-");
  const word = (token: string) => SENSOR_WORDS[token] ?? token;
  const words =
    tokens[0] === "rf" && (tokens[1] === "bias" || tokens[1] === "src")
      ? [word(tokens[1]), "RF", ...tokens.slice(2).map(word)]
      : tokens.map(word);
  const text = words.join(" ");
  const sentence = text.charAt(0).toUpperCase() + text.slice(1);
  return chamber ? `${chamber} ${sentence}` : sentence;
}

/** The display name of a parameter id from parameters.yaml, a sensor id in words, or the id split into words. */
export function parameterLabel(parameter: string): string {
  const known = current.parameters.find((item) => item.id === parameter)?.label;
  if (known) return known;
  const sensor = sensorIdLabel(parameter);
  if (sensor) return sensor;
  const words = parameter.replace(/([A-Z])/g, " $1").trim();
  return words.charAt(0).toUpperCase() + words.slice(1);
}

/** True when the id is a parameter listed in parameters.yaml. */
export function isKnownParameter(parameter: string): boolean {
  return current.parameters.some((item) => item.id === parameter);
}

/** Parameter ids, longest first, for matching inside free text. */
export function parameterIds(): string[] {
  return current.parameters.map((item) => item.id).sort((left, right) => right.length - left.length);
}

/** Replaces raw parameter ids inside server or model text ("cf4Flow shift") with display names ("CF4 flow shift"). */
export function plainParameters(text: string): string {
  const ids = parameterIds();
  if (ids.length === 0) return text;
  parameterPattern ??= new RegExp(`(?<![A-Za-z0-9-])(?:${ids.join("|")})(?![A-Za-z0-9-])`, "g");
  return text.replace(parameterPattern, (id) => parameterLabel(id));
}

const COUNT_WORDS = ["no", "one", "two", "three", "four", "five", "six", "seven", "eight", "nine", "ten"];

/** A count in words for copy ("three plants"), digits past ten, an ellipsis before the catalog loads. */
export function countWord(count: number): string {
  if (count <= 0) return "…";
  return COUNT_WORDS[count] ?? String(count);
}

/** The plants in sites.yaml, in file order. */
export function plants(): { id: string; name: string }[] {
  return current.sites.map((site) => ({ id: site.id, name: site.name }));
}

/** Plant id for a tool. An explicit site id wins; otherwise the longest matching tool prefix from sites.yaml, else the unprefixed plant. */
export function siteForTool(toolId: string, siteId?: string | null): string {
  const explicit = siteId?.trim().toLowerCase();
  if (explicit) return explicit;
  const prefixed = current.sites
    .filter((site) => site.toolPrefix.length > 0 && toolId.startsWith(site.toolPrefix))
    .sort((left, right) => right.toolPrefix.length - left.toolPrefix.length)[0];
  return (prefixed ?? current.sites.find((site) => site.toolPrefix.length === 0))?.id ?? "";
}
