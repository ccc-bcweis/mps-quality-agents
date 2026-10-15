// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company
import type { CSSProperties } from "react";
import type { ProcessStep } from "../api/types";
import { layoutFlow, type FlowBox, type FlowLayout } from "./flowLayout";

const PAD_X = 16;
const PAD_Y = 12;

function boxStyle(box: FlowBox, layout: FlowLayout): CSSProperties {
  return {
    left: `${(box.x / layout.width) * 100}%`,
    top: `${(box.y / layout.height) * 100}%`,
    width: `${(box.w / layout.width) * 100}%`,
    height: `${(box.h / layout.height) * 100}%`,
    fontSize: `${(box.fontSize / layout.width) * 100}cqw`,
    lineHeight: 1.35,
    padding: `${(PAD_Y / layout.width) * 100}cqw ${(PAD_X / layout.width) * 100}cqw`,
  };
}

export function ProcessFlow({ steps, highlighted }: { steps: ProcessStep[]; highlighted: ReadonlySet<string> }) {
  const layout = layoutFlow(steps.map((step) => ({ id: step.id, name: step.name, detail: step.detail })));
  return (
    <div className="floor-canvas relative w-full" style={{ aspectRatio: `${layout.width} / ${layout.height}` }}>
      {layout.boxes.map((box) => {
        const active = highlighted.has(box.id);
        return (
          <div
            key={box.id}
            data-step={box.id}
            data-highlighted={active ? "true" : "false"}
            aria-current={active ? "step" : undefined}
            className={`absolute box-border break-words whitespace-pre-wrap bg-mps-panel ${
              active ? "outline outline-2 outline-mps-nominal" : "outline outline-1 outline-mps-line"
            }`}
            style={boxStyle(box, layout)}
          >
            {box.text}
          </div>
        );
      })}
    </div>
  );
}
