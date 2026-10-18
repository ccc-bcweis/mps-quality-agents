// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company
import { Area, CartesianGrid, ComposedChart, Line, ReferenceArea, ReferenceDot, ReferenceLine, ResponsiveContainer, Tooltip, XAxis, YAxis } from "recharts";
import type { ManualMarker } from "../live/manual";
import { mpsColor } from "../brand/colors";
import { Term } from "./Term";
import { Link } from "react-router-dom";
import { clampLimit, limits, type SpcPoint, type StepLimit } from "../spc/series";
import { btnSm } from "./ui";
import { WAITING_FOR_READINGS } from "../fleet/catalog";
import { limits as catalogLimits, limitText } from "../labels";

export function SpcChart({
  label,
  unit,
  center,
  points: livePoints,
  finding,
  cumulative = false,
  manual,
  steps,
  windowPoints,
}: {
  label: string;
  unit: string;
  /** Center and sigma the server's rules engine checks this series against. Absent for counters. */
  center?: { mean: number; sigma: number; floor?: number; ceiling?: number };
  /** Live samples from the plant. No points: the chart says it is waiting for readings. */
  points?: SpcPoint[];
  /** What the server's rules engine says about this series (from the alert log). */
  finding: string;
  /** A running count. Drawn as a trend with no SPC limits or rule checks. */
  cumulative?: boolean;
  /** A confirmed bench reading, drawn as its own marker. */
  manual?: ManualMarker;
  /**
   * Center and sigma for each point, from the recipe step it was read in: the limits the rules engine scored that
   * point against. When they differ across the window the limits are drawn as steps.
   */
  steps?: StepLimit[];
  /**
   * Readings the window holds (simulation.yaml telemetryWindow.readingsPerSensor). When set, the x axis is always
   * this wide: new readings enter on the right and, once the window is full, the oldest scroll off the left, so the
   * scale and the width of a recipe step never change (#380).
   */
  windowPoints?: number;
}) {
  const points = livePoints ?? [];
  // Until the first live points arrive the chart says so; it never draws a generated series.
  if (points.length === 0) {
    return (
      <figure aria-label={`${label}. ${WAITING_FOR_READINGS}`} className="w-full" data-waiting="true">
        <div className="flex h-[240px] w-full items-center justify-center rounded border border-dashed border-mps-line">
          <p role="status" className="text-mps-muted">
            {WAITING_FOR_READINGS}
          </p>
        </div>
      </figure>
    );
  }
  const values = points.map((point) => point.value);
  // Limits come from the server for this series, the same ones its rules check, so a point drawn past the UCL is a
  // point the rules engine also sees past the control limit.
  const band = center ? limits(center.mean, center.sigma, center.floor, center.ceiling) : null;
  const zone = catalogLimits()?.zoneSigmas ?? [];
  if (cumulative) {
    const trendCaption = `Trend of ${label}, a running count in ${unit}. Counts climb by design, so they have no SPC limits. The rate is charted with limits as run rate.`;
    return (
      <figure aria-label={trendCaption} className="w-full">
        <div aria-hidden="true" className="h-[240px] w-full">
          <ResponsiveContainer width="100%" height={240} initialDimension={{ width: 720, height: 240 }}>
            <ComposedChart data={points} margin={{ top: 16, right: 56, bottom: 8, left: 8 }}>
              <CartesianGrid stroke={mpsColor.line} strokeDasharray="3 3" />
              <XAxis dataKey="index" tick={{ fill: mpsColor.muted, fontSize: 10 }} stroke={mpsColor.line} />
              <YAxis tick={{ fill: mpsColor.muted, fontSize: 10 }} stroke={mpsColor.line} width={64} label={{ value: unit, angle: -90, position: "insideLeft", fill: mpsColor.muted, fontSize: 10 }} />
              <Line type="stepAfter" dataKey="value" stroke={mpsColor.secondary} strokeWidth={1.4} dot={false} isAnimationActive={false} />
            </ComposedChart>
          </ResponsiveContainer>
        </div>
        <figcaption className="mt-2 text-mps-micro text-mps-muted">Running count. No SPC limits; see run rate for the charted rate.</figcaption>
      </figure>
    );
  }
  // A window spans several recipe steps, each with its own nominal: draw each point's own limits as steps, with a
  // divider and the step name at every step change.
  const stepped =
    steps !== undefined &&
    steps.length === points.length &&
    steps.some((step) => step.mean !== steps[0].mean || step.sigma !== steps[0].sigma || stepKey(step) !== stepKey(steps[0]));
  const rows = stepped
    ? points.map((point, i) => {
        const own = steps[i];
        const step = limits(own.mean, own.sigma, own.floor, own.ceiling);
        return {
          ...point,
          mean: step.mean,
          ucl3: step.ucl3,
          lcl3: step.lcl3,
          zone3: [step.lcl3, step.ucl3],
          zone2: [step.lcl2, step.ucl2],
          zone1: [step.lcl1, step.ucl1],
          recipe: own.recipe,
          step: own.step,
          idle: own.idle ?? false,
        };
      })
    : null;
  const runs = stepped ? stepRuns(steps, points, windowPoints) : [];
  const axis = fixedAxis(points, windowPoints);
  const sigma = center?.sigma ?? 0;
  const lastIndex = points.length - 1;
  const perStep = stepped ? " Limits are per recipe step, the same ones the rules check." : "";
  const caption = center
    ? `SPC chart of ${label}. Mean ${center.mean.toFixed(2)} ${unit}, sigma ${center.sigma.toFixed(2)}.${perStep} ${finding}`
    : `SPC chart of ${label}.${perStep} ${finding}`;
  // One sigma of headroom past the limits, but never past what the sensor can read (no negative flow axis).
  const lowest = rows
    ? Math.min(...rows.map((row, i) => clampLimit(row.lcl3 - steps![i].sigma, steps![i].floor)))
    : band
      ? clampLimit(band.lcl3 - sigma, center?.floor)
      : undefined;
  const stepHover = (label: unknown, payload: ReadonlyArray<{ payload?: unknown }> | undefined) =>
    stepHoverText(label, stepped ? (payload?.[0]?.payload as StepRow | undefined) : undefined, unit, sigma);
  const highest = rows
    ? Math.max(...rows.map((row, i) => clampLimit(row.ucl3 + steps![i].sigma, undefined, steps![i].ceiling)))
    : band
      ? clampLimit(band.ucl3 + sigma, undefined, center?.ceiling)
      : undefined;
  const constant = band && !stepped ? band : null;
  // The stepped lines label only their newest point, where the constant lines put their label.
  const edgeLabel = (text: string, color: string) =>
    function EdgeLabel(props: { x?: number | string; y?: number | string; index?: number }) {
      if (props.index !== lastIndex) return <g />;
      return (
        <text x={Number(props.x) + 6} y={Number(props.y)} dy={3} fill={color} fontSize={10}>
          {text}
        </text>
      );
    };

  return (
    <figure aria-label={caption} className="w-full">
      <div aria-hidden="true" className="h-[240px] w-full">
        <ResponsiveContainer width="100%" height={240} initialDimension={{ width: 720, height: 240 }}>
        <ComposedChart data={rows ?? points} margin={{ top: 16, right: 56, bottom: 8, left: 8 }}>
          <CartesianGrid stroke={mpsColor.line} strokeDasharray="3 3" />
          {axis ? (
            <XAxis dataKey="index" type="number" domain={axis.domain} ticks={axis.ticks} allowDataOverflow interval={0} tick={{ fill: mpsColor.muted, fontSize: 10 }} stroke={mpsColor.line} />
          ) : (
            <XAxis dataKey="index" tick={{ fill: mpsColor.muted, fontSize: 10 }} stroke={mpsColor.line} />
          )}
          <YAxis
            domain={[
              Math.min(...(lowest !== undefined ? [lowest] : []), ...values, ...(manual ? [manual.value] : [])),
              Math.max(...(highest !== undefined ? [highest] : []), ...values, ...(manual ? [manual.value] : [])),
            ]}
            tick={{ fill: mpsColor.muted, fontSize: 10 }}
            tickFormatter={(value: number) => axisNumber(value, sigma)}
            tickCount={6}
            stroke={mpsColor.line}
            width={64}
            label={{ value: unit, angle: -90, position: "insideLeft", fill: mpsColor.muted, fontSize: 10 }}
          />
          <Tooltip
            formatter={(value: number | number[], name: string) => [
              Array.isArray(value) ? value.map((v) => axisNumber(v, sigma)).join(" to ") + ` ${unit}` : `${axisNumber(value, sigma)} ${unit}`,
              stepped && name !== "value" ? `${name} (this recipe step)` : name,
            ]}
            labelFormatter={stepHover}
            contentStyle={{ background: mpsColor.panel, border: `1px solid ${mpsColor.line}`, fontSize: 12, maxWidth: 360, whiteSpace: "normal" }}
            labelStyle={{ color: mpsColor.muted }}
          />
          {/* Each reference element must be a direct chart child: Recharts drops ones wrapped in a Fragment (#338). */}
          {constant ? <ReferenceArea y1={constant.lcl3} y2={constant.ucl3} fill={mpsColor.alarm} fillOpacity={0.08} /> : null}
          {constant ? <ReferenceArea y1={constant.lcl2} y2={constant.ucl2} fill={mpsColor.warn} fillOpacity={0.08} /> : null}
          {constant ? <ReferenceArea y1={constant.lcl1} y2={constant.ucl1} fill={mpsColor.nominal} fillOpacity={0.1} /> : null}
          {constant ? <ReferenceLine y={constant.ucl3} stroke={mpsColor.alarm} strokeDasharray="4 3" label={{ value: `UCL ${limitText(zone[2])}σ`, fill: mpsColor.alarm, fontSize: 10, position: "right" }} /> : null}
          {constant ? <ReferenceLine y={constant.lcl3} stroke={mpsColor.alarm} strokeDasharray="4 3" /> : null}
          {constant ? <ReferenceLine y={constant.mean} stroke={mpsColor.muted} label={{ value: "mean", fill: mpsColor.muted, fontSize: 10, position: "right" }} /> : null}
          {/* A divider at every recipe step change (solid where the recipe changes) and a label that never disappears (#366, #376). */}
          {runs
            .filter((run) => run.start > 0)
            .map((run) => (
              <ReferenceLine
                key={`divider-${run.start}`}
                className={run.recipeChange ? "spc-step-divider spc-recipe-divider" : "spc-step-divider"}
                x={points[run.start].index}
                stroke={mpsColor.secondary}
                strokeOpacity={run.recipeChange ? 0.9 : 0.5}
                strokeDasharray={run.recipeChange ? undefined : "2 3"}
                label={run.label ? { value: run.label, position: "insideTopLeft", fill: mpsColor.muted, fontSize: 9 } : undefined}
              />
            ))}
          {runs
            .filter((run) => run.start === 0 && run.label)
            .map((run) => (
              <ReferenceLine
                key="step-first"
                className="spc-step-label"
                x={points[0].index}
                stroke="none"
                label={{ value: run.label, position: "insideTopLeft", fill: mpsColor.muted, fontSize: 9 }}
              />
            ))}
          {runs
            .filter((run) => run.recipeLabel)
            .map((run) => (
              <ReferenceLine
                key={`recipe-${run.start}`}
                className="spc-recipe-label"
                x={points[run.start].index}
                stroke="none"
                label={{ value: run.recipeLabel, position: "insideBottomLeft", fill: mpsColor.secondary, fontSize: 9 }}
              />
            ))}
          {stepped ? <Area type="stepAfter" dataKey="zone3" name={`σ${limitText(zone[2])}`} fill={mpsColor.alarm} fillOpacity={0.08} stroke="none" isAnimationActive={false} /> : null}
          {stepped ? <Area type="stepAfter" dataKey="zone2" name={`σ${limitText(zone[1])}`} fill={mpsColor.warn} fillOpacity={0.08} stroke="none" isAnimationActive={false} /> : null}
          {stepped ? <Area type="stepAfter" dataKey="zone1" name={`σ${limitText(zone[0])}`} fill={mpsColor.nominal} fillOpacity={0.1} stroke="none" isAnimationActive={false} /> : null}
          {stepped ? <Line type="stepAfter" dataKey="ucl3" name={`UCL ${limitText(zone[2])}σ`} stroke={mpsColor.alarm} strokeDasharray="4 3" dot={false} isAnimationActive={false} label={edgeLabel(`UCL ${limitText(zone[2])}σ`, mpsColor.alarm)} /> : null}
          {stepped ? <Line type="stepAfter" dataKey="lcl3" name={`LCL ${limitText(zone[2])}σ`} stroke={mpsColor.alarm} strokeDasharray="4 3" dot={false} isAnimationActive={false} /> : null}
          {stepped ? <Line type="stepAfter" dataKey="mean" name="mean" stroke={mpsColor.muted} dot={false} isAnimationActive={false} label={edgeLabel("mean", mpsColor.muted)} /> : null}
          <Line type="monotone" dataKey="value" stroke={mpsColor.secondary} strokeWidth={1.4} dot={false} isAnimationActive={false} />
          {manual ? (
            <ReferenceDot
              x={points[Math.min(manual.index, points.length - 1)]?.index ?? 0}
              y={manual.value}
              r={7}
              fill={manual.outOfSpec ? mpsColor.alarm : mpsColor.nominal}
              stroke={mpsColor.warn}
              strokeWidth={3}
              label={{ value: manual.clamped ? "Manual (older)" : "Manual", fill: mpsColor.warn, fontSize: 10, position: "top" }}
            />
          ) : null}
        </ComposedChart>
        </ResponsiveContainer>
      </div>
      <figcaption className="mt-2 flex flex-wrap gap-3 text-mps-micro text-mps-muted">
        <span>σ{limitText(zone[0])} <i className="inline-block h-2 w-3 bg-mps-nominal" /></span>
        <span>σ{limitText(zone[1])} <i className="inline-block h-2 w-3 bg-mps-warn" /></span>
        <span>σ{limitText(zone[2])} <i className="inline-block h-2 w-3 bg-mps-alarm" /></span>
        {stepped ? (
          <span data-limits="per-step">
            Limits change with each <Term id="recipe step" spellOut={false} />. A solid line marks where the next recipe starts; its name is at the bottom.{" "}
            <Link className={btnSm} to={`/glossary?q=${encodeURIComponent("recipe step")}`}>
              What is a recipe step?
            </Link>
          </span>
        ) : null}
        <span className="text-mps-secondary">{finding}</span>
        {manual ? (
          <span data-manual-marker="true" className="text-mps-warn">
            {manual.label}.{manual.clamped ? " Observed before this window, so it is pinned to the left edge." : ""}
          </span>
        ) : null}
        <span>
          <Term id="UCL" spellOut={false} /> and <Term id="LCL" spellOut={false} /> are the outer chart lines.
        </span>
      </figcaption>
    </figure>
  );
}

/** Axis numbers with only as many decimals as the spread needs, so labels never run together. */
export function axisNumber(value: number, sigma: number): string {
  if (!Number.isFinite(value)) return "";
  const step = Math.abs(sigma) > 0 ? Math.abs(sigma) : Math.abs(value) || 1;
  const decimals = step >= 10 ? 0 : step >= 1 ? 1 : step >= 0.1 ? 2 : 3;
  return value.toFixed(decimals);
}

/** Identity of a point's recipe step: the segment it belongs to, or its recipe and step name. */
function stepKey(step: StepLimit): string {
  return step.segment !== undefined ? `#${step.segment}` : `${step.recipe ?? ""}|${step.step ?? ""}|${step.mean}|${step.sigma}`;
}

/** Approximate width of the plot and of one label character, used only to decide whether a step name fits. */
const PLOT_WIDTH_PX = 640;
const LABEL_CHAR_PX = 5.5;

/** One recipe step on the chart: where it starts, how many readings it covers, and the labels drawn for it. */
export type StepRun = { start: number; length: number; name: string; label: string; recipeChange: boolean; recipeLabel: string; fits: boolean };

/** The longest candidate that fits in room characters, else the last one cut with an ellipsis, else nothing. */
function fitLabel(candidates: string[], room: number): { text: string; fits: boolean } {
  const texts = candidates.filter((text) => text.length > 0);
  const whole = texts.find((text) => text.length <= room);
  if (whole !== undefined) return { text: whole, fits: true };
  const shortest = texts[texts.length - 1] ?? "";
  return { text: room >= 3 && shortest.length > 0 ? `${shortest.slice(0, room - 1)}…` : "", fits: false };
}

/**
 * The runs of points read in one recipe step. One tool runs several recipes in turn and their steps share names, so
 * where the recipe changes (and at the first run) the run also carries the recipe's name, fitted to the width of the
 * whole recipe (#376). A step label that does not fit is shortened, never dropped: the step, then the step without
 * "(idle)", then cut with an ellipsis. Only a run too narrow for three characters has no label; the hover names it.
 */
export function stepRuns(steps: StepLimit[], points: SpcPoint[], windowPoints?: number): StepRun[] {
  const runs: StepRun[] = [];
  const meta: { recipe?: string; step?: string; idle: boolean }[] = [];
  for (let i = 0; i < steps.length && i < points.length; i += 1) {
    if (i === 0 || stepKey(steps[i]) !== stepKey(steps[i - 1])) {
      const recipeChange = i === 0 || (steps[i].recipe ?? "") !== (steps[i - 1].recipe ?? "");
      const name = [steps[i].step, steps[i].idle ? "(idle)" : ""].filter(Boolean).join(" ");
      runs.push({ start: i, length: 0, name, label: "", recipeChange, recipeLabel: "", fits: false });
      meta.push({ recipe: steps[i].recipe, step: steps[i].step, idle: steps[i].idle ?? false });
    }
    runs[runs.length - 1].length += 1;
  }
  // A fixed axis is the whole window wide, so a step's share of the plot is its share of the window.
  const span = Math.max(1, windowPoints ?? points.length, points.length);
  const room = (length: number) => Math.floor(((length / span) * PLOT_WIDTH_PX) / LABEL_CHAR_PX);
  runs.forEach((run, r) => {
    const step = fitLabel([run.name, meta[r].idle ? (meta[r].step ?? "") : ""], room(run.length));
    run.label = step.text;
    run.fits = step.fits;
    if (!run.recipeChange || !meta[r].recipe) return;
    let span = 0;
    for (let next = r; next < runs.length && (next === r || !runs[next].recipeChange); next += 1) span += runs[next].length;
    run.recipeLabel = fitLabel([meta[r].recipe ?? ""], room(span)).text;
  });
  return runs;
}

/** Ticks on a fixed axis: about this many, on round reading numbers, so they move left with the data. */
const AXIS_TICKS = 12;

/**
 * The x axis for a fixed window (#380): always windowPoints readings wide and ending at the newest reading, so new
 * readings enter on the right and the oldest scroll off the left. Ticks sit on round reading numbers inside the data,
 * so the labels advance as the window scrolls. Undefined without a window (the axis fits the points).
 */
export function fixedAxis(points: SpcPoint[], windowPoints?: number): { domain: [number, number]; ticks: number[] } | undefined {
  if (!windowPoints || windowPoints < 2 || points.length === 0) return undefined;
  const newest = points[points.length - 1].index;
  const oldest = points[0].index;
  const domain: [number, number] = [newest - windowPoints + 1, newest];
  const raw = windowPoints / AXIS_TICKS;
  const magnitude = 10 ** Math.floor(Math.log10(raw));
  const step = [1, 2, 5, 10].map((m) => m * magnitude).find((candidate) => candidate >= raw) ?? raw;
  const ticks: number[] = [];
  for (let tick = Math.ceil(Math.max(domain[0], oldest) / step) * step; tick <= newest; tick += step) ticks.push(tick);
  return { domain, ticks };
}

/** The recipe step of the newest reading, from the same per-reading limits the chart draws (for the chart header). */
export function newestStep(steps: StepLimit[] | undefined): StepLimit | undefined {
  return steps && steps.length > 0 ? steps[steps.length - 1] : undefined;
}

/** The fields of a chart row the hover reads. */
type StepRow = { recipe?: string; step?: string; idle?: boolean; mean?: number; lcl3?: number; ucl3?: number };

/** Hover text for one reading: the recipe step it was read in and that step's center and limits. */
export function stepHoverText(label: unknown, row: StepRow | undefined, unit: string, sigma: number): string {
  if (!row || row.mean === undefined) return `Reading ${String(label)}`;
  const where = [row.recipe, row.step].filter(Boolean).join(" · ");
  const idle = row.idle ? " (idle in this step)" : "";
  return `Reading ${String(label)}${where ? ` · ${where}${idle}` : ""}. Limits for this recipe step: center ${axisNumber(row.mean, sigma)}, LCL ${axisNumber(row.lcl3 ?? Number.NaN, sigma)}, UCL ${axisNumber(row.ucl3 ?? Number.NaN, sigma)} ${unit}.`;
}
