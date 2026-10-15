// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company
import type { ReactNode } from "react";
import type { ToolFamily } from "../api/types";
import { FAMILY_PROCESS } from "../api/labels";
import { mpsColor } from "../brand/colors";
import { fleetSensorNote } from "virtual:fleet-sensors";
import type { FleetSensor } from "../fleet/sensors";
import { sensorReading } from "../fleet/sensors";

type Point = { x: number; y: number };

/** ViewBox pixels. A family omits anchors that are not part of that tool. */
const ANCHORS: Record<ToolFamily, Record<string, Point>> = {
  deposition: {
    "handler.robot": { x: 90, y: 210 },
    "gas.box": { x: 160, y: 90 },
    "gas.flow": { x: 200, y: 160 },
    "chamber.body": { x: 400, y: 180 },
    "chamber.temperature": { x: 520, y: 140 },
    "chuck.heater": { x: 400, y: 280 },
    "rf.generator": { x: 680, y: 120 },
    "rf.forward": { x: 680, y: 210 },
    "vacuum.pump": { x: 400, y: 360 },
    "vacuum.pressure": { x: 250, y: 250 },
  },
  etch: {
    "handler.robot": { x: 90, y: 210 },
    "gas.box": { x: 160, y: 90 },
    "gas.flow": { x: 190, y: 160 },
    "chamber.body": { x: 390, y: 170 },
    "chamber.temperature": { x: 500, y: 130 },
    "chuck.heater": { x: 390, y: 250 },
    "chuck.temperature": { x: 390, y: 210 },
    "rf.generator": { x: 680, y: 110 },
    "rf.forward": { x: 680, y: 190 },
    "rf.reflected": { x: 680, y: 270 },
    "vacuum.pump": { x: 390, y: 360 },
    "vacuum.pressure": { x: 250, y: 280 },
    "vibration.frame": { x: 530, y: 330 },
  },
  strip: {
    "handler.robot": { x: 90, y: 240 },
    "gas.box": { x: 150, y: 80 },
    "gas.flow": { x: 180, y: 140 },
    "rf.generator": { x: 180, y: 220 },
    "chamber.body": { x: 460, y: 180 },
    "chamber.temperature": { x: 520, y: 160 },
    "vacuum.pump": { x: 460, y: 360 },
    "vacuum.pressure": { x: 360, y: 280 },
  },
  clean: {
    "handler.robot": { x: 110, y: 90 },
    "chamber.body": { x: 420, y: 200 },
    "chamber.temperature": { x: 420, y: 230 },
    "bath.megasonic": { x: 420, y: 340 },
    "vacuum.pump": { x: 680, y: 300 },
  },
};

const TONE: Record<string, string> = {
  inSpec: "border-mps-ok text-mps-ok",
  warning: "border-mps-warn text-mps-warn",
  outOfSpec: "border-mps-alarm text-mps-alarm-text",
};

function stroke() {
  return { fill: "none" as const, stroke: mpsColor.line, strokeWidth: 2 };
}

function label(x: number, y: number, text: string) {
  return (
    <text x={x} y={y} fill={mpsColor.muted} fontSize="13">
      {text}
    </text>
  );
}

function Drawing({ family }: { family: ToolFamily }) {
  const name = `${FAMILY_PROCESS[family]} schematic`;
  let body: ReactNode = null;
  if (family === "deposition") {
    body = (
      <g aria-hidden="true">
        <rect x="230" y="80" width="340" height="240" rx="16" {...stroke()} />
        {Array.from({ length: 7 }, (_, index) => (
          <circle key={index} cx={280 + index * 36} cy="120" r="6" fill={mpsColor.nominal} />
        ))}
        {label(330, 150, "Showerhead")}
        <rect x="300" y="260" width="200" height="18" rx="4" fill={mpsColor.nominal} opacity="0.35" />
        {label(360, 300, "Heater")}
        <rect x="620" y="90" width="120" height="150" rx="8" {...stroke()} />
        {label(648, 175, "RF")}
        <rect x="40" y="160" width="80" height="100" rx="8" {...stroke()} />
        {label(48, 280, "Handler")}
        <rect x="330" y="340" width="140" height="36" rx="8" {...stroke()} />
        {label(360, 364, "Pump")}
      </g>
    );
  } else if (family === "etch") {
    body = (
      <g aria-hidden="true">
        <rect x="220" y="90" width="340" height="220" rx="16" {...stroke()} />
        <ellipse cx="390" cy="210" rx="90" ry="28" {...stroke()} stroke={mpsColor.nominal} />
        {label(360, 214, "ESC")}
        <rect x="620" y="80" width="130" height="70" rx="8" {...stroke()} />
        {label(648, 120, "Source RF")}
        <rect x="620" y="160" width="130" height="70" rx="8" {...stroke()} />
        {label(656, 200, "Bias RF")}
        <rect x="40" y="160" width="80" height="100" rx="8" {...stroke()} />
        {label(48, 280, "Handler")}
        <rect x="320" y="340" width="140" height="36" rx="8" {...stroke()} />
        {label(355, 364, "Pump")}
      </g>
    );
  } else if (family === "strip") {
    body = (
      <g aria-hidden="true">
        <circle cx="180" cy="220" r="48" {...stroke()} stroke={mpsColor.nominal} />
        {label(140, 224, "Remote")}
        <line x1="228" y1="220" x2="320" y2="180" stroke={mpsColor.nominal} strokeWidth="3" />
        <rect x="320" y="100" width="280" height="200" rx="16" {...stroke()} />
        {label(400, 200, "Chamber")}
        <rect x="40" y="190" width="80" height="100" rx="8" {...stroke()} />
        {label(48, 310, "Handler")}
        <rect x="390" y="340" width="140" height="36" rx="8" {...stroke()} />
        {label(425, 364, "Pump")}
      </g>
    );
  } else {
    body = (
      <g aria-hidden="true">
        <path d="M250 150 L250 300 L590 300 L590 150" {...stroke()} />
        {label(360, 220, "Open bath")}
        <path d="M280 330 Q350 350 420 330 T560 330" fill="none" stroke={mpsColor.nominal} strokeWidth="3" />
        {label(360, 370, "Megasonic")}
        <rect x="60" y="50" width="90" height="70" rx="8" {...stroke()} />
        {label(72, 90, "Handler")}
      </g>
    );
  }
  return (
    <svg viewBox="0 0 800 420" role="img" aria-label={name} className="h-auto w-full">
      <rect x="0" y="0" width="800" height="420" fill={mpsColor.panel} />
      {body}
    </svg>
  );
}

export function ToolSchematic({
  family,
  sensors,
  selectedId,
  onSelect,
}: {
  family: ToolFamily;
  sensors: FleetSensor[];
  selectedId: string | null;
  onSelect: (sensor: FleetSensor) => void;
}) {
  const placed = ANCHORS[family];
  const onDrawing = sensors.filter((sensor) => placed[sensor.schematicAnchor]);
  const unbound = sensors.filter((sensor) => !placed[sensor.schematicAnchor]);

  return (
    <div>
      <h2 className="mb-2 text-mps-title font-semibold">Tool schematic</h2>
      <p className="mb-2 max-w-3xl text-mps-secondary">
        Each point is a sensor. The color is in spec, warning, or out of spec. Choose a point to open its chart.
      </p>
      <div className="relative">
        <Drawing family={family} />
        {onDrawing.map((sensor) => {
          const point = placed[sensor.schematicAnchor];
          const status = sensor.status ?? "inSpec";
          const selected = sensor.id === selectedId;
          return (
            <button
              key={sensor.id}
              type="button"
              data-anchor={sensor.schematicAnchor}
              data-status={status}
              aria-pressed={selected}
              className={`absolute w-36 -translate-x-1/2 -translate-y-1/2 rounded border bg-mps-panel px-2 py-1 text-left text-mps-micro ${TONE[status]} ${selected ? "ring-2 ring-mps-nominal" : ""}`}
              style={{ left: `${(point.x / 800) * 100}%`, top: `${(point.y / 420) * 100}%` }}
              onClick={() => onSelect(sensor)}
            >
              <span className="block text-mps-secondary">{sensor.label}</span>
              <span className="block font-mono">{sensorReading(sensor)}</span>
            </button>
          );
        })}
      </div>
      <ul className="mt-2 flex flex-wrap gap-3 font-mono text-mps-micro">
        <li className="text-mps-ok">In spec</li>
        <li className="text-mps-warn">Warning</li>
        <li className="text-mps-alarm-text">Out of spec</li>
      </ul>
      {unbound.length > 0 ? (
        <ul className="mt-2 text-mps-secondary">
          {unbound.map((sensor) => (
            <li key={sensor.id}>
              {sensor.label} ({sensor.schematicAnchor}) is not on the drawing.
            </li>
          ))}
        </ul>
      ) : null}
      {fleetSensorNote ? <p className="mt-2 font-mono text-mps-micro text-mps-muted">{fleetSensorNote}</p> : null}
    </div>
  );
}
