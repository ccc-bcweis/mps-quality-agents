// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company
import type { CSSProperties } from "react";
import { Link } from "react-router-dom";
import type { SiteDetail, Throughput, ToolStatus } from "../api/types";
import { mpsColor } from "../brand/colors";
import { Term } from "../components/Term";
import { formatPct, formatPerHour, layoutFloor, statusWord, type FloorLayout, type FloorTool, type LabelBox } from "./floorLayout";

const STATUS_COLOR: Record<ToolStatus, string> = {
  nominal: mpsColor.ok,
  warning: mpsColor.warn,
  alarm: mpsColor.alarm,
};

function boxStyle(box: LabelBox, layout: FloorLayout): CSSProperties {
  return {
    left: `${(box.x / layout.width) * 100}%`,
    top: `${(box.y / layout.height) * 100}%`,
    width: `${(box.w / layout.width) * 100}%`,
    height: `${(box.h / layout.height) * 100}%`,
    fontSize: `${(box.fontSize / layout.width) * 100}cqw`,
    lineHeight: 1.35,
    paddingTop: `${(box.reservedTop / layout.width) * 100}cqw`,
  };
}

function KpiLines({ kpis }: { kpis: Throughput }) {
  return (
    <>
      <p className="whitespace-nowrap">
        <Term id="Wafer starts" spellOut={false} /> {formatPerHour(kpis.waferStartsPerHour)}
        <span> · </span>
        <Term id="Wafer outs" spellOut={false} /> {formatPerHour(kpis.waferOutsPerHour)}
      </p>
      <p className="whitespace-nowrap">
        <Term id="WIP" spellOut={false} /> {kpis.wip}
        <span> · </span>
        <Term id="Yield" spellOut={false} /> {formatPct(kpis.yieldPct)}
        <span> · </span>
        <Term id="Uptime" spellOut={false} /> {formatPct(kpis.uptimePct)}
      </p>
    </>
  );
}

function ToolGlyph({ tool }: { tool: FloorTool }) {
  const color = STATUS_COLOR[tool.status];
  const cx = tool.x + tool.w / 2;
  const cy = tool.y + 16;
  return (
    <g transform={`translate(${cx} ${cy})`}>
      <rect x={-16} y={-12} width={32} height={24} rx={3} fill={mpsColor.panel} stroke={color} strokeWidth={1.6} />
      {tool.family === "deposition" ? <path d="M-8 5 Q0 -7 8 5" fill="none" stroke={mpsColor.nominal} strokeWidth={1.4} /> : null}
      {tool.family === "etch" ? <path d="M-9 1 H-4 L-1 -5 L2 5 L5 0 H9" fill="none" stroke={mpsColor.nominal} strokeWidth={1.4} /> : null}
      {tool.family === "strip" ? <path d="M-7 5 L0 -6 L7 5" fill="none" stroke={mpsColor.nominal} strokeWidth={1.4} /> : null}
      {tool.family === "clean" ? <ellipse cx={0} cy={1} rx={3.2} ry={5} fill="none" stroke={mpsColor.nominal} strokeWidth={1.4} /> : null}
    </g>
  );
}

export function FloorPlan({ site }: { site: SiteDetail }) {
  const layout = layoutFloor(site);
  const markerId = `wafer-flow-${site.id}`;
  const tools = new Map(layout.bays.flatMap((bay) => bay.tools.map((tool) => [tool.id, tool])));

  return (
    <div className="floor-canvas relative w-full" style={{ aspectRatio: `${layout.width} / ${layout.height}` }}>
      <svg
        aria-hidden="true"
        className="absolute inset-0 h-full w-full"
        viewBox={`0 0 ${layout.width} ${layout.height}`}
        preserveAspectRatio="xMidYMid meet"
      >
        <rect width={layout.width} height={layout.height} fill={mpsColor.canvas} />
        <defs>
          <marker id={markerId} markerWidth="8" markerHeight="8" refX="7" refY="3" orient="auto">
            <path d="M0 0 L7 3 L0 6 Z" fill={mpsColor.nominal} />
          </marker>
        </defs>
        {layout.bays.map((bay) => (
          <g key={bay.id}>
            <rect x={bay.x} y={bay.y} width={bay.w} height={bay.h} fill="#141920" stroke={mpsColor.line} />
            <rect
              x={bay.x + 5}
              y={bay.y + 5}
              width={bay.w - 10}
              height={bay.h - 10}
              fill="none"
              stroke={mpsColor.nominal}
              strokeOpacity={0.35}
              strokeDasharray="5 4"
            />
            {bay.tools.map((tool) => (
              <ToolGlyph key={tool.id} tool={tool} />
            ))}
          </g>
        ))}
        {layout.flows.map((flow) => (
          <g key={`${flow.index}-${flow.label}`}>
            <line
              x1={flow.x1}
              y1={flow.y1}
              x2={flow.x2}
              y2={flow.y2}
              stroke={mpsColor.nominal}
              strokeWidth={1.6}
              markerEnd={`url(#${markerId})`}
            />
            <circle cx={flow.badgeX + 13} cy={flow.badgeY + 13} r={12} fill={mpsColor.canvas} stroke={mpsColor.nominal} />
          </g>
        ))}
      </svg>
      <div className="pointer-events-none absolute inset-0">
        {layout.labels.map((label) => {
          const style = boxStyle(label, layout);
          if (label.kind === "tool") {
            const tool = tools.get(label.id.slice("tool-".length));
            if (!tool) return null;
            const word = statusWord(tool.status);
            return (
              <Link
                key={label.id}
                to={`/tools/${tool.id}`}
                data-status={tool.status}
                data-label-id={label.id}
                aria-label={`${tool.name}. Status ${word}. Open the tool schematic.`}
                className="pointer-events-auto absolute flex flex-col items-center text-center no-underline"
                style={style}
              >
                <span className="inline-flex items-center gap-1 whitespace-nowrap font-medium" style={{ color: STATUS_COLOR[tool.status] }}>
                  <span
                    aria-hidden="true"
                    className="inline-block h-[0.7cqw] w-[0.7cqw] shrink-0 rounded-full"
                    style={{ background: STATUS_COLOR[tool.status] }}
                  />
                  {word}
                </span>
                <span className="whitespace-nowrap font-mono text-mps-secondary">{tool.id}</span>
              </Link>
            );
          }
          if (label.kind === "kpi") {
            const bay = site.bays.find((item) => label.id === `kpi-${item.id}`);
            if (!bay) return null;
            return (
              <div key={label.id} data-label-id={label.id} className="pointer-events-auto absolute text-mps-secondary" style={style}>
                <KpiLines kpis={bay.kpis} />
              </div>
            );
          }
          const align = label.kind === "badge" ? "flex items-center justify-center font-mono text-mps-nominal" : "whitespace-nowrap";
          const tone = label.kind === "bay" ? "font-semibold text-mps-text" : "text-mps-secondary";
          return (
            <div key={label.id} data-label-id={label.id} className={`pointer-events-auto absolute ${align} ${tone}`} style={style}>
              {label.text}
            </div>
          );
        })}
      </div>
    </div>
  );
}
