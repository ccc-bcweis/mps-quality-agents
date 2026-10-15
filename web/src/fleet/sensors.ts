// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company
import type { ToolFamily } from "../api/types";

export type SensorLimits = {
  minimum: number;
  maximum: number;
  nominal: number;
  warningMinimum: number;
  warningMaximum: number;
};

export type FleetSensor = {
  id: string;
  toolId: string;
  family: ToolFamily;
  type: string;
  unit: string;
  subsystem: string;
  schematicAnchor: string;
  chamberId?: string;
  parameter?: string;
  tracksSensorId?: string;
  limits: SensorLimits;
};

const FAMILIES = new Set<ToolFamily>(["deposition", "etch", "strip", "clean"]);
const REQUIRED = ["id", "type", "unit", "subsystem", "schematicAnchor"] as const;

type Raw = Record<string, string>;

function clean(value: string): string {
  const trimmed = value.trim();
  if (trimmed.startsWith('"') || trimmed.startsWith("'")) {
    const quote = trimmed[0];
    const end = trimmed.indexOf(quote, 1);
    if (end > 0) return trimmed.slice(1, end);
  }
  const comment = trimmed.indexOf(" #");
  return (comment >= 0 ? trimmed.slice(0, comment) : trimmed).trim();
}

function isFamily(value: string): value is ToolFamily {
  return FAMILIES.has(value as ToolFamily);
}

/** Tool ids under the root `tools:` list. Recipe ids above that list are ignored. */
export function toolIdsInFleetYaml(yaml: string): string[] {
  const ids: string[] = [];
  let inTools = false;
  for (const line of yaml.split(/\r?\n/)) {
    if (/^tools:\s*(#.*)?$/.test(line)) {
      inTools = true;
      continue;
    }
    if (inTools && /^\S/.test(line) && !line.trim().startsWith("#")) break;
    if (!inTools) continue;
    const match = line.match(/^ {2}- id:\s*(\S+)/);
    if (match) ids.push(clean(match[1]));
  }
  return ids;
}

function readFamily(lines: string[]): ToolFamily | null {
  for (const line of lines) {
    const match = line.match(/^family:\s*(\S+)/);
    if (!match) continue;
    const value = clean(match[1]);
    if (!isFamily(value)) throw new Error(`Fleet family ${value} is unknown.`);
    return value;
  }
  return null;
}

function parseFlow(body: string): Raw {
  const inner = body.trim().replace(/^\{/, "").replace(/\}\s*$/, "");
  const raw: Raw = {};
  const pattern = /([A-Za-z][A-Za-z0-9]*)\s*:\s*("(?:[^"\\]|\\.)*"|'(?:[^'\\]|\\.)*'|[^,{}]+)/g;
  for (const match of inner.matchAll(pattern)) raw[match[1]] = clean(match[2]);
  return raw;
}

function parseItems(block: string[]): Raw[] {
  const items: { head: string; lines: string[] }[] = [];
  for (const line of block) {
    if (line.trim() === "" || line.trim().startsWith("#")) continue;
    const dash = line.match(/^\s*-\s*(.*)$/);
    if (dash) {
      items.push({ head: dash[1], lines: [] });
      continue;
    }
    if (items.length === 0) continue;
    items[items.length - 1].lines.push(line);
  }
  return items.map((item) => {
    if (item.head.trim().startsWith("{")) return parseFlow(item.head);
    const raw: Raw = {};
    const head = item.head.match(/^([A-Za-z][A-Za-z0-9]*)\s*:\s*(.*)$/);
    if (head) raw[head[1]] = clean(head[2]);
    for (const line of item.lines) {
      const field = line.match(/^\s*([A-Za-z][A-Za-z0-9]*)\s*:\s*(.*)$/);
      if (field) raw[field[1]] = clean(field[2]);
    }
    return raw;
  });
}

function requireText(raw: Raw, key: string): string {
  const value = raw[key];
  if (!value) throw new Error(`Fleet sensor ${raw.id || "(no id)"} is missing ${key}.`);
  return value;
}

function requireNumber(raw: Raw, key: string): number {
  if (raw[key] === undefined || raw[key] === "") {
    throw new Error(`Fleet sensor ${raw.id || "(no id)"} is missing ${key}.`);
  }
  const value = Number(raw[key]);
  if (!Number.isFinite(value)) throw new Error(`Fleet sensor ${raw.id} has a ${key} that is not a number.`);
  return value;
}

function toSensor(raw: Raw, toolId: string, family: ToolFamily): FleetSensor {
  for (const key of REQUIRED) requireText(raw, key);
  const limits: SensorLimits = {
    minimum: requireNumber(raw, "minimum"),
    maximum: requireNumber(raw, "maximum"),
    nominal: requireNumber(raw, "nominal"),
    warningMinimum: requireNumber(raw, "warningMinimum"),
    warningMaximum: requireNumber(raw, "warningMaximum"),
  };
  if (limits.minimum > limits.maximum) {
    throw new Error(`Fleet sensor ${raw.id} has a minimum above its maximum.`);
  }
  if (limits.nominal < limits.minimum || limits.nominal > limits.maximum) {
    throw new Error(`Fleet sensor ${raw.id} has a nominal outside the spec.`);
  }
  if (
    limits.warningMinimum < limits.minimum ||
    limits.warningMaximum > limits.maximum ||
    limits.warningMinimum > limits.nominal ||
    limits.warningMaximum < limits.nominal
  ) {
    throw new Error(`Fleet sensor ${raw.id} has a warning band outside the spec.`);
  }
  return {
    id: raw.id,
    toolId,
    family,
    type: raw.type,
    unit: raw.unit,
    subsystem: raw.subsystem,
    schematicAnchor: raw.schematicAnchor,
    chamberId: raw.chamberId || undefined,
    parameter: raw.parameter || undefined,
    tracksSensorId: raw.tracksSensorId || undefined,
    limits,
  };
}

/**
 * Sensors nested under each tool. A file with no tool `sensors:` list contributes nothing.
 * One-line flow mappings and flat blocks are both accepted. Folded scalars are not.
 */
export function parseFleetSensors(yaml: string): FleetSensor[] {
  const lines = yaml.split(/\r?\n/);
  const family = readFamily(lines);
  const sensors: FleetSensor[] = [];
  let inTools = false;
  let currentTool: string | null = null;

  for (let index = 0; index < lines.length; index += 1) {
    const line = lines[index];
    if (/^tools:\s*(#.*)?$/.test(line)) {
      inTools = true;
      currentTool = null;
      continue;
    }
    if (inTools && line.trim() !== "" && !line.trim().startsWith("#") && !/^\s/.test(line)) {
      inTools = false;
      currentTool = null;
    }
    if (!inTools) continue;

    const tool = line.match(/^ {2}- id:\s*(\S+)/);
    if (tool) {
      currentTool = clean(tool[1]);
      continue;
    }
    if (!/^ {4}sensors:\s*(#.*)?$/.test(line)) continue;
    if (!currentTool) throw new Error("A sensors list is outside a tool.");
    if (!family) throw new Error("Fleet file has sensors but no family.");

    const block: string[] = [];
    let next = index + 1;
    for (; next < lines.length; next += 1) {
      const row = lines[next];
      if (row.trim() === "" || row.trim().startsWith("#")) {
        block.push(row);
        continue;
      }
      const indent = row.match(/^( *)/)?.[1].length ?? 0;
      if (indent <= 4) break;
      block.push(row);
    }
    for (const raw of parseItems(block)) sensors.push(toSensor(raw, currentTool, family));
    index = next - 1;
  }

  return sensors;
}

export function statusFromLimits(value: number, limits: SensorLimits): "inSpec" | "warning" | "outOfSpec" {
  if (value < limits.minimum || value > limits.maximum) return "outOfSpec";
  if (value < limits.warningMinimum || value > limits.warningMaximum) return "warning";
  return "inSpec";
}
