// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company
import type { ReactNode } from "react";
import type { ToolFamily } from "../api/types";
import { FAMILY_PROCESS } from "../api/labels";
import { mpsColor } from "../brand/colors";
import { anchorBoxes, anchorById, CHAMBER_SHIFT, SCHEMATIC_VIEW, type AnchorBox } from "../fleet/schematic";

const stroke = { fill: "none" as const, stroke: mpsColor.line, strokeWidth: 2 };
const dim = { fill: mpsColor.muted, fontSize: 14, fontFamily: "Inter, Segoe UI, sans-serif" };

function caption(x: number, y: number, text: string) {
  return (
    <text x={x} y={y} fill={dim.fill} fontSize={dim.fontSize} fontFamily={dim.fontFamily}>
      {text}
    </text>
  );
}

type Frame = { x: number; y: number; w: number; h: number };

function bounds(boxes: AnchorBox[], pad: { l: number; r: number; t: number; b: number }): Frame {
  const left = Math.min(...boxes.map((box) => box.x - box.w / 2)) - pad.l;
  const right = Math.max(...boxes.map((box) => box.x + box.w / 2)) + pad.r;
  const top = Math.min(...boxes.map((box) => box.y - box.h / 2)) - pad.t;
  const bottom = Math.max(...boxes.map((box) => box.y + box.h / 2)) + pad.b;
  return { x: left, y: top, w: right - left, h: bottom - top };
}

function Showerhead({ frame }: { frame: Frame }) {
  const y = frame.y + 28;
  const left = frame.x + 56;
  const right = frame.x + frame.w - 120;
  const holes = 8;
  return (
    <g>
      <line x1={left} y1={y} x2={right} y2={y} stroke={mpsColor.nominal} strokeWidth={4} />
      {Array.from({ length: holes }, (_, index) => (
        <circle key={index} cx={left + ((right - left) * index) / (holes - 1)} cy={y + 14} r={4} fill={mpsColor.nominal} />
      ))}
      {caption(left, y - 10, "Showerhead")}
    </g>
  );
}

function MatchCoil({ x, y }: { x: number; y: number }) {
  return (
    <g>
      <path
        d={`M ${x} ${y} q 8 -14 16 0 t 16 0 t 16 0 t 16 0`}
        fill="none"
        stroke={mpsColor.nominal}
        strokeWidth={2}
      />
      <line x1={x - 8} y1={y - 16} x2={x - 8} y2={y + 16} stroke={mpsColor.line} strokeWidth={2} />
      <line x1={x + 72} y1={y - 16} x2={x + 72} y2={y + 16} stroke={mpsColor.line} strokeWidth={2} />
      {caption(x, y - 22, "Match")}
    </g>
  );
}

function Pump({ x, y }: { x: number; y: number }) {
  return (
    <g>
      <circle cx={x} cy={y} r={26} {...stroke} />
      <path d={`M ${x - 8} ${y - 10} L ${x + 12} ${y} L ${x - 8} ${y + 10} Z`} fill={mpsColor.nominal} opacity={0.8} />
      {caption(x - 18, y + 44, "Pump")}
    </g>
  );
}

function Throttle({ x, y }: { x: number; y: number }) {
  return (
    <g>
      <circle cx={x} cy={y} r={14} {...stroke} />
      <line x1={x - 10} y1={y - 10} x2={x + 10} y2={y + 10} stroke={mpsColor.nominal} strokeWidth={2} />
      {caption(x + 20, y + 4, "Throttle")}
    </g>
  );
}

function Gauge({ x, y }: { x: number; y: number }) {
  return (
    <g>
      <circle cx={x} cy={y} r={16} {...stroke} />
      <line x1={x} y1={y} x2={x + 8} y2={y - 9} stroke={mpsColor.nominal} strokeWidth={2} />
    </g>
  );
}

function GasSticks({ gases, manifoldY }: { gases: AnchorBox[]; manifoldY: number }) {
  if (gases.length === 0) return null;
  const left = Math.min(...gases.map((gas) => gas.x));
  const right = Math.max(...gases.map((gas) => gas.x));
  return (
    <g>
      {caption(left - 8, manifoldY - 28, "Gas box")}
      <line x1={left} y1={manifoldY} x2={right} y2={manifoldY} stroke={mpsColor.nominal} strokeWidth={3} />
      {gases.map((gas) => (
        <g key={gas.anchor}>
          <line x1={gas.x} y1={manifoldY} x2={gas.x} y2={gas.y - gas.h / 2 - 4} stroke={mpsColor.nominal} strokeWidth={2} />
          <rect x={gas.x - 8} y={manifoldY + 10} width={16} height={22} rx={2} {...stroke} />
        </g>
      ))}
    </g>
  );
}

function ColumnArt({ family }: { family: Exclude<ToolFamily, "clean"> }) {
  const map = anchorById(family);
  const at = (suffix: string) => map[`a.${suffix}`];
  const pressure = at("chamber.pressure");
  const wall = at("chamber.wall");
  const chuck = at("chuck.esc") ?? at("chuck.heater") ?? at("chuck.temperature");
  const edge = at("chuck.edgeRing");
  const vibration = at("vacuum.vibration");
  const gases = Object.values(map).filter((box) => box.anchor.startsWith("a.gas."));
  const upper = [pressure, wall, chuck, edge].filter((box): box is AnchorBox => Boolean(box));
  const frame = bounds(upper, { l: 36, r: 36, t: 48, b: 20 });
  const mid = frame.x + frame.w / 2;
  const forward = at("rf.source.forward") ?? at("rf.forward") ?? at("rf.remote.forward");
  const reflected = at("rf.source.reflected") ?? at("rf.reflected") ?? at("rf.remote.reflected");
  const bias = at("rf.bias.forward");

  let body: ReactNode = null;
  if (family === "etch" && chuck) {
    body = (
      <g>
        <ellipse cx={chuck.x} cy={chuck.y + 8} rx={120} ry={42} {...stroke} stroke={mpsColor.nominal} />
        <ellipse cx={chuck.x} cy={chuck.y + 8} rx={78} ry={24} {...stroke} />
        {caption(chuck.x - 16, chuck.y + 46, "ESC")}
      </g>
    );
  } else if (family === "deposition" && chuck) {
    body = (
      <g>
        <rect x={chuck.x - 110} y={chuck.y - 8} width={220} height={28} rx={4} fill={mpsColor.nominal} opacity={0.25} />
        <rect x={chuck.x - 110} y={chuck.y - 8} width={220} height={28} rx={4} {...stroke} />
        {caption(chuck.x - 28, chuck.y + 40, "Heater")}
      </g>
    );
  } else if (chuck) {
    body = (
      <g>
        <rect x={chuck.x - 100} y={chuck.y - 6} width={200} height={22} rx={3} {...stroke} stroke={mpsColor.nominal} />
        {caption(chuck.x - 24, chuck.y + 36, "Platen")}
      </g>
    );
  }

  return (
    <g>
      {family === "strip" ? (
        <g>
          <circle cx={frame.x - 8} cy={frame.y + 90} r={42} {...stroke} stroke={mpsColor.nominal} />
          <circle cx={frame.x - 8} cy={frame.y + 90} r={16} fill={mpsColor.nominal} opacity={0.35} />
          {caption(frame.x - 70, frame.y + 150, "Remote plasma")}
          <line
            x1={frame.x + 34}
            y1={frame.y + 90}
            x2={frame.x}
            y2={frame.y + 70}
            stroke={mpsColor.nominal}
            strokeWidth={4}
          />
        </g>
      ) : null}
      <rect x={frame.x} y={frame.y} width={frame.w} height={frame.h} rx={18} {...stroke} />
      <path
        d={`M ${frame.x + 8} ${frame.y + frame.h * 0.45} h -16`}
        stroke={mpsColor.line}
        strokeWidth={6}
      />
      {family === "strip" ? null : <Showerhead frame={frame} />}
      {family === "etch" ? (
        <ellipse cx={mid} cy={frame.y + frame.h * 0.48} rx={70} ry={28} fill={mpsColor.nominal} opacity={0.12} />
      ) : null}
      {pressure ? <Gauge x={pressure.x - pressure.w / 2 - 22} y={pressure.y} /> : null}
      {wall ? (
        <g>
          <circle cx={frame.x + frame.w - 18} cy={wall.y} r={5} fill={mpsColor.nominal} />
          <line x1={frame.x + frame.w - 18} y1={wall.y} x2={wall.x + wall.w / 2 - 6} y2={wall.y} stroke={mpsColor.line} />
        </g>
      ) : null}
      {body}
      {caption(frame.x + 16, frame.y + 22, "Chamber")}
      {gases.length > 0 ? <GasSticks gases={gases} manifoldY={gases[0].y - gases[0].h / 2 - 36} /> : null}
      {gases.length > 0 ? (
        <line
          x1={gases[0].x}
          y1={gases[0].y - gases[0].h / 2 - 36}
          x2={mid}
          y2={frame.y + 28}
          stroke={mpsColor.nominal}
          strokeWidth={2}
        />
      ) : null}
      {forward && reflected ? (
        <MatchCoil x={(forward.x + reflected.x) / 2 - 36} y={(forward.y + reflected.y) / 2} />
      ) : null}
      {family === "etch" && forward && bias ? (
        <g>
          {caption(forward.x - forward.w / 2, forward.y - 36, "Source")}
          {caption(forward.x - forward.w / 2, bias.y - 36, "Bias")}
        </g>
      ) : null}
      {vibration ? (
        <g>
          <line x1={mid} y1={frame.y + frame.h} x2={mid} y2={vibration.y} stroke={mpsColor.line} strokeWidth={3} />
          <Throttle x={mid} y={vibration.y - 70} />
          <Pump x={vibration.x - 150} y={vibration.y} />
          <line
            x1={vibration.x - 124}
            y1={vibration.y}
            x2={vibration.x - vibration.w / 2 - 4}
            y2={vibration.y}
            stroke={mpsColor.line}
            strokeWidth={3}
          />
        </g>
      ) : null}
    </g>
  );
}

function LoadLock() {
  return (
    <g>
      {caption(36, 32, "Load lock")}
      <rect x={36} y={44} width={188} height={250} rx={12} {...stroke} />
      <rect x={58} y={70} width={64} height={150} {...stroke} />
      {Array.from({ length: 6 }, (_, index) => (
        <line key={index} x1={66} y1={88 + index * 20} x2={114} y2={88 + index * 20} stroke={mpsColor.nominal} strokeWidth={2} />
      ))}
      {caption(58, 64, "Cassette")}
      <path d="M 140 150 H 210 V 120 H 250" fill="none" stroke={mpsColor.nominal} strokeWidth={3} />
      <circle cx={210} cy={150} r={6} fill={mpsColor.nominal} />
      {caption(36, 360, "Wafer handler")}
    </g>
  );
}

function Counter() {
  return (
    <g>
      {caption(40, 888, "Wafer counter")}
      <rect x={40} y={900} width={1520} height={76} rx={10} {...stroke} />
    </g>
  );
}

function CleanArt() {
  const map = anchorById("clean");
  const bath = map["a.chuck.bath"];
  const mega = map["a.chuck.megasonic"];
  const pressure = map["a.chamber.pressure"];
  return (
    <g>
      {caption(36, 32, "Load lock")}
      <rect x={36} y={48} width={160} height={180} rx={12} {...stroke} />
      <rect x={56} y={78} width={48} height={110} {...stroke} />
      {caption(56, 72, "Cassette")}
      <path d="M 196 130 H 280" fill="none" stroke={mpsColor.nominal} strokeWidth={3} />
      {caption(280, 48, "Bath")}
      <path d="M 300 90 H 1100 V 520 H 300 Z" {...stroke} />
      <path d="M 320 430 H 1080" fill="none" stroke={mpsColor.nominal} strokeWidth={3} opacity={0.7} />
      <path d="M 320 400 Q 500 380 700 400 T 1080 400" fill="none" stroke={mpsColor.nominal} strokeWidth={2} />
      {Array.from({ length: 6 }, (_, index) => (
        <line
          key={index}
          x1={420 + index * 90}
          y1={110}
          x2={420 + index * 90}
          y2={150}
          stroke={mpsColor.nominal}
          strokeWidth={2}
        />
      ))}
      {caption(420, 104, "Spray bar")}
      {bath ? (
        <g>
          <ellipse cx={bath.x} cy={bath.y + 70} rx={70} ry={16} {...stroke} stroke={mpsColor.nominal} />
          {caption(bath.x - 40, bath.y + 100, "Spin chuck")}
        </g>
      ) : null}
      {mega ? (
        <g>
          {Array.from({ length: 4 }, (_, index) => (
            <rect key={index} x={mega.x - 80 + index * 42} y={mega.y + 40} width={28} height={10} fill={mpsColor.nominal} opacity={0.45} />
          ))}
          {caption(mega.x - 40, mega.y + 68, "Megasonic")}
        </g>
      ) : null}
      {pressure ? <Gauge x={pressure.x - pressure.w / 2 - 24} y={pressure.y} /> : null}
      {caption(1160, 70, "Chemical manifold")}
      <line x1={1240} y1={120} x2={1240} y2={400} stroke={mpsColor.nominal} strokeWidth={3} />
      <line x1={1240} y1={160} x2={1100} y2={140} stroke={mpsColor.nominal} strokeWidth={2} />
      {caption(1160, 470, "Drain pump")}
      <Pump x={1240} y={560} />
      <line x1={700} y1={520} x2={700} y2={600} stroke={mpsColor.line} strokeWidth={3} />
      <line x1={700} y1={600} x2={1214} y2={560} stroke={mpsColor.line} strokeWidth={3} />
    </g>
  );
}

export function SchematicDrawing({ family }: { family: ToolFamily }) {
  const boxes = anchorBoxes(family);
  return (
    <svg
      viewBox={`0 0 ${SCHEMATIC_VIEW.width} ${SCHEMATIC_VIEW.height}`}
      role="img"
      aria-label={`${FAMILY_PROCESS[family]} schematic`}
      className="block h-auto w-full"
    >
      <rect width={SCHEMATIC_VIEW.width} height={SCHEMATIC_VIEW.height} fill={mpsColor.panel} />
      <g aria-hidden="true">
        {family === "clean" ? (
          <CleanArt />
        ) : (
          <g>
            <LoadLock />
            <ColumnArt family={family} />
            <g transform={`translate(${CHAMBER_SHIFT} 0)`}>
              <ColumnArt family={family} />
            </g>
          </g>
        )}
        <Counter />
      </g>
      {boxes.map((box) => (
        <circle key={box.anchor} data-schematic-anchor={box.anchor} cx={box.x} cy={box.y} r={3.5} fill={mpsColor.nominal} />
      ))}
    </svg>
  );
}
