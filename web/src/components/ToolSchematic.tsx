// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company
import type { ToolFamily } from "../api/types";
import { Explain } from "./Explain";
import { Term } from "./Term";
import type { FleetSensor } from "../fleet/sensors";
import { lookupTerm } from "../glossary/terms";
import { anchorById, SCHEMATIC_VIEW, sensorLabel, slotWord } from "../fleet/schematic";
import { formatValue, readingFor, type SensorStatus } from "../fleet/telemetry";
import { SchematicDrawing } from "./schematicArt";

const TONE: Record<SensorStatus, string> = {
  inSpec: "border-mps-ok text-mps-ok",
  warning: "border-mps-warn text-mps-warn",
  outOfSpec: "border-mps-alarm text-mps-alarm-text",
};

const DOT: Record<SensorStatus, string> = {
  inSpec: "bg-mps-ok",
  warning: "bg-mps-warn",
  outOfSpec: "bg-mps-alarm",
};

function SensorWords({ text }: { text: string }) {
  if (lookupTerm(text)) return <Term id={text} spellOut={false} focusable={false} />;
  const parts = text.split(/(\s+)/);
  return parts.map((part, index) =>
    lookupTerm(part) ? (
      <Term key={`${part}-${index}`} id={part} spellOut={false} focusable={false} />
    ) : (
      <span key={`${part}-${index}`}>{part}</span>
    ),
  );
}

function Unit({ unit }: { unit: string }) {
  if (!lookupTerm(unit)) return unit;
  return <Term id={unit} spellOut={false} focusable={false} />;
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
  const placed = anchorById(family);
  const onDrawing = sensors.filter((sensor) => placed[sensor.schematicAnchor]);
  const unbound = sensors.filter((sensor) => !placed[sensor.schematicAnchor]);

  return (
    <div>
      <h2 className="mb-2 text-mps-title font-semibold">Tool schematic</h2>
      <p className="mb-2 max-w-4xl text-mps-secondary">
        Each point is a sensor on this tool. The color is in spec, warning, or out of spec, from that sensor&apos;s spec limits. Choose a
        point to open its <Explain text="SPC" /> chart.
      </p>
      <div className="overflow-x-auto">
        <div className="relative min-w-[1600px]">
          <SchematicDrawing family={family} />
          <div className="absolute inset-0">
            {onDrawing.map((sensor) => {
              const point = placed[sensor.schematicAnchor];
              const reading = readingFor(sensor);
              const selected = sensor.id === selectedId;
              return (
                <button
                  key={sensor.id}
                  type="button"
                  data-anchor={sensor.schematicAnchor}
                  data-sensor-id={sensor.id}
                  data-status={reading.status}
                  aria-pressed={selected}
                  className={`absolute flex -translate-x-1/2 -translate-y-1/2 flex-col justify-center overflow-hidden rounded border bg-mps-panel/95 px-1.5 py-0.5 text-left leading-tight focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-mps-nominal ${TONE[reading.status]} ${selected ? "ring-2 ring-mps-nominal" : ""}`}
                  style={{
                    left: `${(point.x / SCHEMATIC_VIEW.width) * 100}%`,
                    top: `${(point.y / SCHEMATIC_VIEW.height) * 100}%`,
                    width: `${(point.w / SCHEMATIC_VIEW.width) * 100}%`,
                    height: `${(point.h / SCHEMATIC_VIEW.height) * 100}%`,
                  }}
                  onClick={() => onSelect(sensor)}
                >
                  <span className="flex items-center gap-1 truncate text-[11px] text-mps-secondary">
                    <span className={`inline-block h-2 w-2 shrink-0 rounded-full ${DOT[reading.status]}`} />
                    <span className="font-mono">{slotWord(sensor.schematicAnchor)}</span>
                    <SensorWords text={sensorLabel(sensor.schematicAnchor)} />
                  </span>
                  <span className="truncate font-mono text-[12px]">
                    {formatValue(reading.value, reading.unit)} <Unit unit={reading.unit} />
                  </span>
                  <span className="truncate font-mono text-[10px] text-mps-muted">{sensor.id}</span>
                </button>
              );
            })}
          </div>
        </div>
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
              {sensorLabel(sensor.schematicAnchor)} ({sensor.schematicAnchor}) is not on the drawing.
            </li>
          ))}
        </ul>
      ) : null}
    </div>
  );
}
