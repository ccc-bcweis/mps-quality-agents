// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

export type SensorStatus = "inSpec" | "warning" | "outOfSpec";

export type FleetSensor = {
  id: string;
  toolId: string;
  type: string;
  unit: string;
  subsystem: string;
  schematicAnchor: string;
  label: string;
  value?: number;
  display?: string;
  status?: SensorStatus;
};

const REQUIRED = ["id", "type", "unit", "subsystem", "schematicAnchor"] as const;
const STATUSES = new Set<SensorStatus>(["inSpec", "warning", "outOfSpec"]);

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
    const match = line.match(/^\s+-\s+id:\s*(\S+)/);
    if (match) ids.push(clean(match[1]));
  }
  return ids;
}

function sensorLines(yaml: string): string[] | null {
  const lines = yaml.split(/\r?\n/);
  const start = lines.findIndex((line) => /^sensors:\s*(#.*)?$/.test(line));
  if (start < 0) return null;
  const block: string[] = [];
  for (const line of lines.slice(start + 1)) {
    if (line.trim() !== "" && !line.trim().startsWith("#") && !/^\s/.test(line)) break;
    block.push(line);
  }
  return block;
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

function expand(raw: Raw, toolIds: string[]): FleetSensor[] {
  for (const key of REQUIRED) {
    if (!raw[key]) throw new Error(`Fleet sensor is missing ${key}.`);
  }
  if (raw.status && !STATUSES.has(raw.status as SensorStatus)) {
    throw new Error(`Fleet sensor ${raw.id} has an unknown status.`);
  }
  let value: number | undefined;
  if (raw.value) {
    value = Number(raw.value);
    if (Number.isNaN(value)) throw new Error(`Fleet sensor ${raw.id} has a value that is not a number.`);
  }
  const owners = raw.toolId ? [raw.toolId] : toolIds;
  if (owners.length === 0) throw new Error(`Fleet sensor ${raw.id} has no tool id.`);
  return owners.map((toolId) => ({
    id: raw.toolId ? raw.id : `${raw.id}:${toolId}`,
    toolId,
    type: raw.type,
    unit: raw.unit,
    subsystem: raw.subsystem,
    schematicAnchor: raw.schematicAnchor,
    label: raw.label || raw.type,
    value,
    display: raw.display,
    status: raw.status as SensorStatus | undefined,
  }));
}

/**
 * Root `sensors:` list only. A file with no such list contributes nothing.
 * A sensor without toolId is copied onto each tool in that file.
 * Supports a flat block list or a one-line flow mapping. Folded scalars are not supported.
 */
export function parseFleetSensors(yaml: string): FleetSensor[] {
  const block = sensorLines(yaml);
  if (!block) return [];
  const toolIds = toolIdsInFleetYaml(yaml);
  return parseItems(block).flatMap((raw) => expand(raw, toolIds));
}

export function sensorReading(sensor: FleetSensor): string {
  if (sensor.display) return sensor.display;
  if (sensor.value !== undefined) return `${sensor.value} ${sensor.unit}`;
  return "—";
}
