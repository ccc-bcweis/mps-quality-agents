// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company
import type { CSSProperties } from "react";
import type { ToolFamily } from "../api/types";
import { Explain } from "./Explain";
import { Term } from "./Term";
import type { FleetSensor } from "../fleet/sensors";
import { lookupTerm } from "../glossary/terms";
import {
  HANDLER_PANEL_PX,
  chamberGridTemplate,
  counterGridTemplate,
  schematicPanels,
  sensorLabel,
  slotWord,
  type AnchorBox,
  type SchematicPanel,
  type ViewBox,
} from "../fleet/schematic";
import { formatValue, readingFor, type SensorStatus } from "../fleet/telemetry";
import { PanelDrawing } from "./schematicArt";
import { FAMILY_PROCESS } from "../api/labels";

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

function boxStyle(box: AnchorBox, view: ViewBox): CSSProperties {
  return {
    left: `${(box.x / view.width) * 100}%`,
    top: `${(box.y / view.height) * 100}%`,
    width: `${(box.w / view.width) * 100}%`,
    height: `${(box.h / view.height) * 100}%`,
  };
}

function SensorButton({
  sensor,
  selected,
  onSelect,
  className,
  style,
}: {
  sensor: FleetSensor;
  selected: boolean;
  onSelect: (sensor: FleetSensor) => void;
  className: string;
  style?: CSSProperties;
}) {
  const reading = readingFor(sensor);
  return (
    <button
      type="button"
      data-anchor={sensor.schematicAnchor}
      data-sensor-id={sensor.id}
      data-status={reading.status}
      aria-pressed={selected}
      className={`flex min-w-0 flex-col justify-center rounded border bg-mps-panel/95 px-1.5 py-0.5 text-left text-[11px] leading-tight focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-mps-nominal ${TONE[reading.status]} ${selected ? "ring-2 ring-mps-nominal" : ""} ${className}`}
      style={style}
      onClick={() => onSelect(sensor)}
    >
      <span className="block text-mps-secondary">
        <span className={`mr-1 inline-block h-2 w-2 rounded-full align-middle ${DOT[reading.status]}`} />
        <span className="font-mono">{slotWord(sensor.schematicAnchor)}</span> <SensorWords text={sensorLabel(sensor.schematicAnchor)} />
      </span>
      <span className="block break-words font-mono text-[12px] leading-tight">
        {formatValue(reading.value, reading.unit)} <Unit unit={reading.unit} />
      </span>
      <span className="block break-words font-mono text-[11px] leading-tight text-mps-muted">{sensor.id}</span>
    </button>
  );
}

function FlowPanel({
  panel,
  sensors,
  selectedId,
  onSelect,
  className,
}: {
  panel: SchematicPanel;
  sensors: FleetSensor[];
  selectedId: string | null;
  onSelect: (sensor: FleetSensor) => void;
  className?: string;
}) {
  const byAnchor = new Map(sensors.map((sensor) => [sensor.schematicAnchor, sensor]));
  return (
    <section className={className} aria-label={panel.title}>
      <h3 className="mb-1 text-mps-body font-semibold">{panel.title}</h3>
      <div className="relative w-full" style={{ aspectRatio: `${panel.view.width} / ${panel.view.height}` }}>
        <PanelDrawing panel={panel} />
        <div className="absolute inset-0">
          {panel.boxes.map((box) => {
            const sensor = byAnchor.get(box.anchor);
            if (!sensor) return null;
            return (
              <SensorButton
                key={sensor.id}
                sensor={sensor}
                selected={sensor.id === selectedId}
                onSelect={onSelect}
                className="absolute -translate-x-1/2 -translate-y-1/2"
                style={boxStyle(box, panel.view)}
              />
            );
          })}
        </div>
      </div>
    </section>
  );
}

function CounterStrip({
  panel,
  sensors,
  selectedId,
  onSelect,
}: {
  panel: SchematicPanel;
  sensors: FleetSensor[];
  selectedId: string | null;
  onSelect: (sensor: FleetSensor) => void;
}) {
  const wanted = new Set(panel.boxes.map((box) => box.anchor));
  const cards = sensors.filter((sensor) => wanted.has(sensor.schematicAnchor));
  return (
    <section aria-label={panel.title}>
      <h3 className="mb-1 text-mps-body font-semibold">{panel.title}</h3>
      <div className="grid gap-3" data-counter-grid="" style={{ gridTemplateColumns: counterGridTemplate() }}>
        {cards.map((sensor) => (
          <SensorButton
            key={sensor.id}
            sensor={sensor}
            selected={sensor.id === selectedId}
            onSelect={onSelect}
            className="w-full"
          />
        ))}
      </div>
      <svg aria-hidden="true" className="sr-only" viewBox={`0 0 ${panel.view.width} ${panel.view.height}`}>
        {panel.boxes.map((box) => (
          <circle key={box.anchor} data-schematic-anchor={box.anchor} cx={box.x} cy={box.y} r={3.5} />
        ))}
      </svg>
    </section>
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
  const panels = schematicPanels(family);
  const known = new Set(panels.flatMap((panel) => panel.boxes.map((box) => box.anchor)));
  const unbound = sensors.filter((sensor) => !known.has(sensor.schematicAnchor));
  const handler = panels.find((panel) => panel.kind === "handler");
  const chambers = panels.filter((panel) => panel.kind === "chamber" || panel.kind === "station");
  const counters = panels.find((panel) => panel.kind === "counters");

  return (
    <section aria-label={`${FAMILY_PROCESS[family]} schematic`} className="min-w-0">
      <h2 className="mb-2 text-mps-title font-semibold">Tool schematic</h2>
      <p className="mb-2 max-w-4xl text-mps-secondary">
        Each point is a sensor on this tool. The color is in spec, warning, or out of spec, from that sensor&apos;s spec limits. Choose a
        point to open its <Explain text="SPC" /> chart.
      </p>
      <div className="grid gap-3">
        {handler ? (
          <div className="w-full" style={{ maxWidth: HANDLER_PANEL_PX }}>
            <FlowPanel panel={handler} sensors={sensors} selectedId={selectedId} onSelect={onSelect} className="w-full" />
          </div>
        ) : null}
        <div className="grid w-full min-w-0 gap-3" data-chamber-grid="" style={{ gridTemplateColumns: chamberGridTemplate() }}>
          {chambers.map((panel) => (
            <FlowPanel key={panel.id} panel={panel} sensors={sensors} selectedId={selectedId} onSelect={onSelect} className="min-w-0" />
          ))}
        </div>
        {counters ? <CounterStrip panel={counters} sensors={sensors} selectedId={selectedId} onSelect={onSelect} /> : null}
      </div>
      <ul className="mt-2 flex flex-wrap gap-3 text-[11px] leading-tight">
        <li className="text-mps-ok">In spec</li>
        <li className="text-mps-warn">Warning</li>
        <li className="text-mps-alarm-text">Out of spec</li>
      </ul>
      {unbound.length > 0 ? (
        <ul className="mt-2 text-mps-body text-mps-secondary">
          {unbound.map((sensor) => (
            <li key={sensor.id}>
              {sensorLabel(sensor.schematicAnchor)} ({sensor.schematicAnchor}) is not on the drawing.
            </li>
          ))}
        </ul>
      ) : null}
    </section>
  );
}
