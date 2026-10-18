// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company
import { cloneElement, type ReactElement } from "react";
import { render, screen } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import { SpcChart } from "./SpcChart";
import { mpsColor } from "../brand/colors";
import { pointLimits } from "../live/overlay";
import { mergeSegments } from "../live/stream";
import type { ToolLive } from "../api/types";

// jsdom has no layout, so give the chart a fixed size instead of measuring its container.
vi.mock("recharts", async (importOriginal) => {
  const actual = await importOriginal<typeof import("recharts")>();
  return {
    ...actual,
    ResponsiveContainer: ({ children }: { children: ReactElement<{ width?: number; height?: number }> }) =>
      cloneElement(children, { width: 720, height: 240 }),
  };
});

// Live (Oct 17): E-01 chamber pressure spans recipe steps with nominals 45, 40 and 35 in one 600-point window.
const nominals = [45, 40, 35];
const perStep = 20;
const points = Array.from({ length: perStep * nominals.length }, (_, index) => ({
  index,
  value: nominals[Math.floor(index / perStep)] + Math.sin(index) * 0.3,
}));
const steps = points.map((point) => ({ mean: nominals[Math.floor(point.index / perStep)], sigma: 0.5 }));

/** The distinct y values a Recharts curve passes through. */
function levels(path: string | null | undefined): number[] {
  const ys = [...(path ?? "").matchAll(/[ML]\s*([-\d.]+)\s*,\s*([-\d.]+)/g)].map((match) => Math.round(Number(match[2])));
  return [...new Set(ys)];
}

describe("SpcChart limits per recipe step", () => {
  it("draws stepped UCL, LCL, mean and sigma bands that follow the recipe step", () => {
    const { container } = render(
      <SpcChart label="Chamber pressure" unit="mTorr" center={{ mean: 35, sigma: 0.5 }} steps={steps} points={points} finding="No active SPC alert." />,
    );
    // One constant limit for the whole window would put earlier steps' healthy points outside the band.
    expect(container.querySelectorAll(".recharts-reference-line")).toHaveLength(0);
    expect(container.querySelectorAll(".recharts-reference-area")).toHaveLength(0);
    expect(container.querySelectorAll(".recharts-area")).toHaveLength(3);
    const curves = [...container.querySelectorAll(".recharts-line-curve")];
    const ucl = curves.filter((curve) => curve.getAttribute("stroke") === mpsColor.alarm);
    expect(ucl).toHaveLength(2); // UCL and LCL, same color and dash as before.
    for (const line of ucl) expect(levels(line.getAttribute("d"))).toHaveLength(nominals.length);
    const mean = curves.find((curve) => curve.getAttribute("stroke") === mpsColor.muted);
    expect(levels(mean?.getAttribute("d"))).toHaveLength(nominals.length);
    for (const area of container.querySelectorAll(".recharts-area-area")) expect(area.getAttribute("d")).toBeTruthy();
    // Same labels as the constant lines, and the legend and summary say the limits follow the recipe step.
    expect(container.textContent).toMatch(/UCL 3σ/);
    expect(container.textContent).toMatch(/mean/);
    expect(container.querySelector("[data-limits='per-step']")?.textContent).toBe("Limits follow the recipe step.");
    expect(screen.getByRole("figure").getAttribute("aria-label")).toMatch(/Limits are per recipe step/);
  });

  it("keeps the constant lines when every point shares one recipe step's limits", () => {
    const flat = points.map(() => ({ mean: 35, sigma: 0.5 }));
    const { container } = render(<SpcChart label="Chamber pressure" unit="mTorr" center={{ mean: 35, sigma: 0.5 }} steps={flat} points={points} finding="x" />);
    expect(container.querySelectorAll(".recharts-reference-line")).toHaveLength(3);
    expect(container.querySelectorAll(".recharts-reference-area")).toHaveLength(3);
    expect(container.querySelector("[data-limits='per-step']")).toBeNull();
  });

  it("gives each point the limits of the segment it was read in", () => {
    const at = (second: number) => new Date(Date.UTC(2026, 9, 17, 0, 0, second)).toISOString();
    const live = {
      toolId: "E-01",
      series: [
        {
          parameter: "chamberPressure",
          chamberId: "E-01-A",
          points: [0, 50, 119, 120, 239, 240].map((second) => ({ at: at(second), value: 1 })),
          center: 35,
          sigma: 0.5,
          segments: [
            { from: at(0), center: 45, sigma: 0.6 },
            { from: at(120), center: 40, sigma: 0.5 },
            { from: at(240), center: 35, sigma: 0.5 },
          ],
        },
      ],
    } as unknown as ToolLive;
    expect(pointLimits(live, "chamberPressure", "E-01-A")?.map((limit) => limit.mean)).toEqual([45, 45, 45, 40, 40, 35]);
  });

  it("merges streamed segments in order and drops the ones before the oldest kept point", () => {
    const at = (second: number) => new Date(Date.UTC(2026, 9, 17, 0, 0, second)).toISOString();
    const previous = [
      { from: at(0), center: 45, sigma: 0.6 },
      { from: at(120), center: 40, sigma: 0.5 },
    ];
    const merged = mergeSegments(previous, [{ from: at(120), center: 40, sigma: 0.5 }, { from: at(240), center: 35, sigma: 0.5 }], at(130));
    expect(merged).toEqual([
      { from: at(120), center: 40, sigma: 0.5 },
      { from: at(240), center: 35, sigma: 0.5 },
    ]);
    expect(mergeSegments(previous, [{ from: at(60), center: 1, sigma: 1 }], at(0))).toEqual(previous);
  });
});
