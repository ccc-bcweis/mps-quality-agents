// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company
import { cloneElement, type ReactElement } from "react";
import { render as rtlRender, screen } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { describe, expect, it, vi } from "vitest";
import { SpcChart, fixedAxis, stepHoverText, stepRuns } from "./SpcChart";
import { mpsColor } from "../brand/colors";
import { pointLimits, seriesPoints } from "../live/overlay";
import { applyToolDelta, mergeSegments } from "../live/stream";
import { limits } from "../spc/series";
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

/** The chart has a glossary button, so it renders inside a router. */
const render = (element: ReactElement) => rtlRender(<MemoryRouter>{element}</MemoryRouter>);

// Live (Oct 17): E-01 chamber pressure spans recipe steps with nominals 45, 40 and 35 in one 600-point window.
const nominals = [45, 40, 35];
const perStep = 20;
const points = Array.from({ length: perStep * nominals.length }, (_, index) => ({
  index,
  value: nominals[Math.floor(index / perStep)] + Math.sin(index) * 0.3,
}));
const stepNames = ["Stabilize", "Main etch", "Endpoint"];
const steps = points.map((point) => {
  const step = Math.floor(point.index / perStep);
  return { mean: nominals[step], sigma: 0.5, recipe: "Oxide main etch", step: stepNames[step], segment: step };
});

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
    // One constant limit for the whole window would put earlier steps' healthy points outside the band. The only
    // reference lines are the step dividers, the first step's label and the recipe name, no constant UCL/LCL/mean.
    expect(container.querySelectorAll(".recharts-reference-line")).toHaveLength(nominals.length + 1);
    expect(container.querySelectorAll(".spc-step-divider")).toHaveLength(nominals.length - 1);
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
    expect(container.querySelector("[data-limits='per-step']")?.textContent).toMatch(/^Limits change with each recipe step\./);
    const glossary = screen.getByRole("link", { name: "What is a recipe step?" });
    expect(glossary.getAttribute("href")).toBe("/glossary?q=recipe%20step");
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

  it("marks every recipe step change with a divider and names each step where it fits (#366)", () => {
    const { container } = render(
      <SpcChart label="ESC temperature" unit="C" center={{ mean: 35, sigma: 0.5 }} steps={steps} points={points} finding="x" />,
    );
    const dividers = [...container.querySelectorAll(".spc-step-divider")];
    expect(dividers).toHaveLength(stepNames.length - 1);
    for (const name of stepNames) expect(container.textContent).toContain(name);
    // Two steps with the same limits are still two steps: the divider comes from the step, not from the limits.
    const sameLimits = steps.map((step) => ({ ...step, mean: 50, sigma: 1 }));
    const again = render(<SpcChart label="ESC temperature" unit="C" center={{ mean: 50, sigma: 1 }} steps={sameLimits} points={points} finding="x" />);
    expect(again.container.querySelectorAll(".spc-step-divider")).toHaveLength(stepNames.length - 1);
    // A step too short for its name keeps the divider; a one-reading run has no room for a label (the hover names it).
    const runs = stepRuns(
      points.map((point) => ({ mean: 50, sigma: 1, step: point.index === 30 ? "Short" : "Long step", segment: point.index === 30 ? 1 : point.index < 30 ? 0 : 2 })),
      points,
    );
    expect(runs.map((run) => [run.name, run.fits, run.label])).toEqual([
      ["Long step", true, "Long step"],
      ["Short", false, ""],
      ["Long step", true, "Long step"],
    ]);
  });

  // Live (Oct 18, #376): E-02 runs Oxide main etch then Polysilicon etch, three steps each, 8 readings per step at
  // 1x / 15 s. Oxide raises the ESC to 52 C in Main etch; Polysilicon holds 49 C (idle) in Main etch and Endpoint.
  const cycle = [
    { recipe: "Oxide main etch", step: "Stabilize", mean: 50, sigma: 8 / 6, idle: false },
    { recipe: "Oxide main etch", step: "Main etch", mean: 52, sigma: 8 / 6, idle: false },
    { recipe: "Oxide main etch", step: "Endpoint", mean: 50, sigma: 8 / 6, idle: true },
    { recipe: "Polysilicon etch", step: "Stabilize", mean: 49, sigma: 10 / 6, idle: false },
    { recipe: "Polysilicon etch", step: "Main etch", mean: 49, sigma: 10 / 6, idle: true },
    { recipe: "Polysilicon etch", step: "Endpoint", mean: 49, sigma: 10 / 6, idle: true },
  ];
  const twoCycles = Array.from({ length: 96 }, (_, index) => ({ index, value: 50 }));
  const twoCycleSteps = twoCycles.map((point) => {
    const segment = Math.floor(point.index / 8);
    return { ...cycle[segment % cycle.length], segment };
  });

  it("names the recipe where it changes and never drops a step label (#376)", () => {
    const runs = stepRuns(twoCycleSteps, twoCycles);
    expect(runs).toHaveLength(12);
    expect(runs.every((run) => run.label.length > 0)).toBe(true);
    expect(runs.filter((run) => run.recipeChange).map((run) => run.start)).toEqual([0, 24, 48, 72]);
    expect(runs.filter((run) => run.recipeLabel).map((run) => run.recipeLabel)).toEqual(["Oxide main etch", "Polysilicon etch", "Oxide main etch", "Polysilicon etch"]);
    // "Main etch (idle)" is too long for 8 of 96 readings: it is shortened, not dropped, and never shown as Stabilize.
    const polyMain = runs[4];
    expect(polyMain.name).toBe("Main etch (idle)");
    expect(polyMain.label.startsWith("Main etch") || polyMain.label.startsWith("Main")).toBe(true);
    // Every label names its own run's step: labels and limits come from the same per-reading segment.
    runs.forEach((run) => {
      const step = twoCycleSteps[run.start];
      expect(run.name.startsWith(step.step)).toBe(true);
      const labelStep = run.label.replace("…", "");
      expect(step.step.startsWith(labelStep) || labelStep.startsWith(step.step)).toBe(true);
    });
    const wide = stepRuns(twoCycleSteps.slice(24, 48), twoCycles.slice(24, 48));
    expect(wide[0].label).toBe("Stabilize");
    expect(wide[0].recipeLabel).toBe("Polysilicon etch");
    expect(wide[1].label).toBe("Main etch (idle)");
  });

  it("draws a solid divider and the recipe name where the recipe changes (#376)", () => {
    const { container } = render(
      <SpcChart label="ESC temperature" unit="C" center={{ mean: 49, sigma: 10 / 6 }} steps={twoCycleSteps.slice(16, 64)} points={twoCycles.slice(0, 48)} finding="x" />,
    );
    expect(container.querySelectorAll(".spc-step-divider")).toHaveLength(5);
    expect(container.querySelectorAll(".spc-recipe-divider")).toHaveLength(2);
    // The window opens in Oxide Endpoint, then Polysilicon (3 steps), then Oxide again: three recipe names.
    expect([...container.querySelectorAll(".spc-recipe-label")].map((node) => node.textContent)).toEqual(["Oxide main etch", "Polysilicon etch", "Oxide main etch"]);
    expect(container.textContent).toContain("Main etch (idle)");
    expect(container.textContent).toContain("A solid line marks where the next recipe starts; its name is at the bottom.");
  });

  it("hover says the limits are for this recipe step, with the step's center and limits", () => {
    const row = { recipe: "Oxide main etch", step: "Main etch", idle: false, mean: 52, lcl3: 48, ucl3: 56 };
    expect(stepHoverText(12, row, "C", 1.3)).toBe("Reading 12 · Oxide main etch · Main etch. Limits for this recipe step: center 52.0, LCL 48.0, UCL 56.0 C.");
    expect(stepHoverText(3, { ...row, step: "Endpoint", idle: true }, "C", 1.3)).toContain("Endpoint (idle in this step)");
  });

  it("clamps every limit to what the sensor can physically read: no negative limits (#368)", () => {
    // Bias RF reflected on live: center 10.5 W, sigma 12.5 W, sensor 0 to 75 W. Unclamped LCL was -27 W.
    const band = limits(10.5, 12.5, 0, 75);
    expect(band.lcl3).toBe(0);
    expect(band.lcl2).toBe(0);
    expect(band.ucl3).toBe(48);
    const flow = points.map((point) => ({ index: point.index, value: 4 + Math.sin(point.index) }));
    const clamped = points.map((point) => ({ mean: point.index < 30 ? 5 : 10.5, sigma: point.index < 30 ? 3 : 12.5, floor: 0, ceiling: 75, segment: point.index < 30 ? 0 : 1, step: point.index < 30 ? "A" : "B" }));
    const { container } = render(<SpcChart label="Bias RF reflected" unit="W" center={{ mean: 10.5, sigma: 12.5, floor: 0, ceiling: 75 }} steps={clamped} points={flow} finding="x" />);
    const ticks = [...container.querySelectorAll(".recharts-yAxis .recharts-cartesian-axis-tick-value")].map((tick) => Number(tick.textContent));
    expect(ticks.length).toBeGreaterThan(0);
    expect(Math.min(...ticks)).toBeGreaterThanOrEqual(0);
  });

  it("keeps the recipe step limits during and after a fault: limits never follow the faulty readings (#373)", () => {
    // SC-1 on E-02 chamber A at 10x: readings every second, the fault starts mid-step and runs across two step changes.
    const at = (second: number) => new Date(Date.UTC(2026, 9, 18, 6, 0, second)).toISOString();
    const segment = (second: number, center: number, step: string) => ({ from: at(second), center, sigma: 4 / 3, recipe: "Oxide main etch", step, idle: false, floor: 40, ceiling: 60 });
    const first = {
      toolId: "E-02",
      siteId: "northwater",
      simulatedNow: at(59),
      status: "nominal",
      readings: [],
      series: [
        {
          parameter: "escTemperature",
          chamberId: "E-02-A",
          points: Array.from({ length: 60 }, (_, second) => ({ at: at(second), value: 50 })),
          center: 50,
          sigma: 4 / 3,
          segments: [segment(0, 50, "Stabilize")],
        },
      ],
    } as unknown as ToolLive;
    let live: ToolLive | null = first;
    // The fault: readings jump to about 55 and swing; one-second deltas cross 120 s (Main etch) and 240 s (Endpoint).
    for (let second = 60; second < 300; second += 1) {
      const step = second < 120 ? segment(second, 50, "Stabilize") : second < 240 ? segment(second, 52, "Main etch") : segment(second, 50, "Endpoint");
      live = applyToolDelta(live, {
        toolId: "E-02",
        siteId: "northwater",
        simulatedNow: at(second),
        status: "alarm",
        readings: [],
        series: [{ parameter: "escTemperature", chamberId: "E-02-A", points: [{ at: at(second), value: 55 + 4 * Math.sin(second / 3) }], center: step.center, sigma: step.sigma, segments: [step] }],
        resync: false,
      });
    }
    const limitsByPoint = pointLimits(live, "escTemperature", "E-02-A")!;
    expect(limitsByPoint).toHaveLength(300);
    const centers = limitsByPoint.map((limit) => limit.mean);
    // Every point keeps the center of the step it was read in; nothing is recomputed from the faulty readings.
    expect(centers.slice(0, 120).every((center) => center === 50)).toBe(true);
    expect(centers.slice(120, 240).every((center) => center === 52)).toBe(true);
    expect(centers.slice(240).every((center) => center === 50)).toBe(true);
    expect(new Set(limitsByPoint.map((limit) => limit.step))).toEqual(new Set(["Stabilize", "Main etch", "Endpoint"]));
    expect(new Set(limitsByPoint.map((limit) => limit.segment)).size).toBe(3);
  });
});

describe("SpcChart fixed-width x axis (#380)", () => {
  const WINDOW = 600;
  const at = (second: number) => new Date(Date.UTC(2026, 9, 18, 7, 0, 0) + second * 1000).toISOString();
  const names = ["Stabilize", "Main etch", "Endpoint"];
  const segment = (second: number) => {
    const n = Math.floor(second / 120);
    return { from: at(n * 120), center: n % 3 === 1 ? 52 : 50, sigma: 4 / 3, recipe: "Oxide main etch", step: names[n % 3], idle: n % 3 === 2, floor: 40, ceiling: 60 };
  };
  const tool = (seconds: number[], windowPoints = WINDOW) =>
    ({
      toolId: "E-02",
      siteId: "northwater",
      simulatedNow: at(seconds[seconds.length - 1]),
      status: "nominal",
      readings: [],
      windowPoints,
      series: [
        {
          parameter: "escTemperature",
          chamberId: "E-02-A",
          center: 50,
          sigma: 4 / 3,
          points: seconds.map((second) => ({ at: at(second), value: 50 })),
          segments: [...new Map(seconds.map((second) => [segment(second).from, segment(second)])).values()],
        },
      ],
    }) as unknown as ToolLive;
  const delta = (second: number) => ({
    toolId: "E-02",
    siteId: "northwater",
    simulatedNow: at(second),
    status: "nominal" as const,
    readings: [],
    series: [{ parameter: "escTemperature", chamberId: "E-02-A", points: [{ at: at(second), value: 50 }], center: 50, sigma: 4 / 3, segments: [segment(second)] }],
    resync: false,
  });
  /** x pixel of each step divider, from the drawn reference lines. */
  const dividerX = (container: HTMLElement) =>
    [...container.querySelectorAll(".spc-step-divider line")].map((line) => Number(line.getAttribute("x1")));
  const chart = (live: ToolLive) => {
    const points = seriesPoints(live, "escTemperature", "E-02-A")!;
    const steps = pointLimits(live, "escTemperature", "E-02-A")!;
    return render(<SpcChart label="ESC temperature" unit="C" center={{ mean: 50, sigma: 4 / 3 }} steps={steps} points={points} windowPoints={live.windowPoints ?? undefined} finding="x" />);
  };

  it("is always the full window wide: the domain never rescales while the window fills", () => {
    const few = fixedAxis(Array.from({ length: 100 }, (_, index) => ({ index, value: 1 })), WINDOW)!;
    expect(few.domain).toEqual([-500, 99]);
    expect(few.ticks.every((tick) => tick >= 0 && tick <= 99)).toBe(true);
    const full = fixedAxis(Array.from({ length: 600 }, (_, index) => ({ index, value: 1 })), WINDOW)!;
    expect(full.domain[1] - full.domain[0]).toBe(few.domain[1] - few.domain[0]);
    expect(fixedAxis([{ index: 0, value: 1 }], undefined)).toBeUndefined();
  });

  it("keeps a recipe step the same width as readings arrive: dividers only move left", () => {
    let live: ToolLive | null = tool(Array.from({ length: 250 }, (_, second) => second));
    const before = dividerX(chart(live).container);
    expect(before).toHaveLength(2); // 120 and 240
    for (let second = 250; second < 370; second += 1) live = applyToolDelta(live, delta(second));
    const after = dividerX(chart(live!).container);
    expect(after).toHaveLength(3); // 120, 240, 360
    // Same distance between the 120 and 240 dividers: the scale did not change, the whole plot moved left.
    expect(after[1] - after[0]).toBeCloseTo(before[1] - before[0], 3);
    expect(after[0]).toBeLessThan(before[0]);
  });

  it("scrolls once the window is full: reading numbers advance and old dividers leave on the left", () => {
    let live: ToolLive | null = tool(Array.from({ length: WINDOW }, (_, second) => second));
    const full = chart(live);
    const firstTicks = [...full.container.querySelectorAll(".recharts-xAxis .recharts-cartesian-axis-tick-value")].map((tick) => Number(tick.textContent));
    expect(firstTicks[0]).toBe(0);
    expect(dividerX(full.container)).toHaveLength(4); // 120, 240, 360, 480
    for (let second = WINDOW; second < WINDOW + 130; second += 1) live = applyToolDelta(live, delta(second));
    const series = live!.series[0];
    expect(series.points).toHaveLength(WINDOW);
    expect(series.first).toBe(130);
    const points = seriesPoints(live, "escTemperature", "E-02-A")!;
    expect(points[0].index).toBe(130);
    expect(points[points.length - 1].index).toBe(729);
    const scrolled = chart(live!);
    const ticks = [...scrolled.container.querySelectorAll(".recharts-xAxis .recharts-cartesian-axis-tick-value")].map((tick) => Number(tick.textContent));
    expect(ticks[0]).toBeGreaterThanOrEqual(130);
    expect(ticks[ticks.length - 1]).toBeLessThanOrEqual(729);
    // The 120 divider scrolled off the left; 240, 360 and 480 moved left; 600 and 720 came in on the right.
    const before = dividerX(full.container);
    const after = dividerX(scrolled.container);
    expect(after).toHaveLength(5);
    expect(after[0]).toBeCloseTo(before[1] - (before[1] - before[0]) * (130 / 120), 3);
    expect(fixedAxis(points, WINDOW)!.domain).toEqual([130, 729]);
  });
});
