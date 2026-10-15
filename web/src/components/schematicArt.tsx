// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company
import type { ArtPiece, SchematicPanel } from "../fleet/schematic";
import { artPieces } from "../fleet/schematic";
import { mpsColor } from "../brand/colors";

const stroke = { fill: "none" as const, stroke: mpsColor.line, strokeWidth: 2 };
const ink = { fill: mpsColor.muted, fontSize: 14, fontFamily: "Inter, Segoe UI, sans-serif" };

function DrawPiece({ piece }: { piece: ArtPiece }) {
  switch (piece.kind) {
    case "text":
      return (
        <text x={piece.x} y={piece.y + 14} fill={ink.fill} fontSize={ink.fontSize} fontFamily={ink.fontFamily}>
          {piece.label}
        </text>
      );
    case "rect":
      return <rect x={piece.x} y={piece.y} width={piece.w} height={piece.h} rx={12} {...stroke} />;
    case "line":
      return piece.w >= piece.h ? (
        <line x1={piece.x} y1={piece.y + piece.h / 2} x2={piece.x + piece.w} y2={piece.y + piece.h / 2} stroke={mpsColor.nominal} strokeWidth={3} />
      ) : (
        <line x1={piece.x + piece.w / 2} y1={piece.y} x2={piece.x + piece.w / 2} y2={piece.y + piece.h} stroke={mpsColor.nominal} strokeWidth={3} />
      );
    case "circle":
      return <circle cx={piece.x + piece.w / 2} cy={piece.y + piece.h / 2} r={Math.min(piece.w, piece.h) / 2} {...stroke} />;
    case "ellipse":
      return (
        <ellipse cx={piece.x + piece.w / 2} cy={piece.y + piece.h / 2} rx={piece.w / 2} ry={piece.h / 2} {...stroke} stroke={mpsColor.nominal} />
      );
    case "showerhead":
      return (
        <g>
          <line x1={piece.x} y1={piece.y + 2} x2={piece.x + piece.w} y2={piece.y + 2} stroke={mpsColor.nominal} strokeWidth={3} />
          {Array.from({ length: 7 }, (_, index) => (
            <circle key={index} cx={piece.x + (piece.w * index) / 6} cy={piece.y + 6} r={1.5} fill={mpsColor.nominal} />
          ))}
        </g>
      );
    case "match":
      return (
        <path
          d={`M ${piece.x} ${piece.y + piece.h / 2} q 6 -8 12 0 t 12 0 t 12 0`}
          fill="none"
          stroke={mpsColor.nominal}
          strokeWidth={2}
        />
      );
    case "pump":
      return (
        <g>
          <circle cx={piece.x + piece.w / 2} cy={piece.y + piece.h / 2} r={Math.min(piece.w, piece.h) / 2 - 1} {...stroke} />
          <path
            d={`M ${piece.x + piece.w * 0.35} ${piece.y + piece.h * 0.32} L ${piece.x + piece.w * 0.7} ${piece.y + piece.h / 2} L ${piece.x + piece.w * 0.35} ${piece.y + piece.h * 0.68} Z`}
            fill={mpsColor.nominal}
            opacity={0.8}
          />
        </g>
      );
    case "throttle":
      return (
        <g>
          <circle cx={piece.x + piece.w / 2} cy={piece.y + piece.h / 2} r={Math.min(piece.w, piece.h) / 2 - 1} {...stroke} />
          <line
            x1={piece.x + 6}
            y1={piece.y + 6}
            x2={piece.x + piece.w - 6}
            y2={piece.y + piece.h - 6}
            stroke={mpsColor.nominal}
            strokeWidth={2}
          />
        </g>
      );
    case "gauge":
      return (
        <g>
          <circle cx={piece.x + piece.w / 2} cy={piece.y + piece.h / 2} r={Math.min(piece.w, piece.h) / 2 - 1} {...stroke} />
          <line
            x1={piece.x + piece.w / 2}
            y1={piece.y + piece.h / 2}
            x2={piece.x + piece.w * 0.75}
            y2={piece.y + piece.h * 0.28}
            stroke={mpsColor.nominal}
            strokeWidth={2}
          />
        </g>
      );
    case "plasma":
      return (
        <g>
          <circle cx={piece.x + piece.w / 2} cy={piece.y + piece.h / 2} r={Math.min(piece.w, piece.h) / 2 - 1} {...stroke} stroke={mpsColor.nominal} />
          <circle cx={piece.x + piece.w / 2} cy={piece.y + piece.h / 2} r={Math.min(piece.w, piece.h) / 5} fill={mpsColor.nominal} opacity={0.4} />
        </g>
      );
    case "megasonic":
      return (
        <g>
          {Array.from({ length: 4 }, (_, index) => (
            <rect key={index} x={piece.x + index * (piece.w / 4)} y={piece.y} width={piece.w / 4 - 6} height={piece.h} fill={mpsColor.nominal} opacity={0.45} />
          ))}
        </g>
      );
    default:
      return null;
  }
}

export function PanelDrawing({ panel }: { panel: SchematicPanel }) {
  const pieces = artPieces(panel);
  return (
    <svg
      viewBox={`0 0 ${panel.view.width} ${panel.view.height}`}
      role="img"
      aria-label={`${panel.title} schematic`}
      className="absolute inset-0 h-full w-full"
    >
      <rect width={panel.view.width} height={panel.view.height} fill={mpsColor.panel} />
      <g aria-hidden="true">
        {pieces.map((piece) => (
          <DrawPiece key={piece.id} piece={piece} />
        ))}
      </g>
      {panel.boxes.map((box) => (
        <circle key={box.anchor} data-schematic-anchor={box.anchor} cx={box.x} cy={box.y} r={3.5} fill={mpsColor.nominal} />
      ))}
    </svg>
  );
}
