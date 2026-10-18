// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company
import { Area, CartesianGrid, ComposedChart, Line, ReferenceArea, ReferenceDot, ReferenceLine, ResponsiveContainer, Tooltip, XAxis, YAxis } from "recharts";
import type { ManualMarker } from "../live/manual";
import { mpsColor } from "../brand/colors";
import { Term } from "./Term";
import { limits, type SpcPoint } from "../spc/series";
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
}: {
  label: string;
  unit: string;
  /** Center and sigma the server's rules engine checks this series against. Absent for counters. */
  center?: { mean: number; sigma: number };
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
  steps?: { mean: number; sigma: number }[];
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
  const band = center ? limits(center.mean, center.sigma) : null;
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
  // A window spans several recipe steps, each with its own nominal: draw each point's own limits as steps.
  const stepped =
    steps !== undefined && steps.length === points.length && steps.some((step) => step.mean !== steps[0].mean || step.sigma !== steps[0].sigma);
  const rows = stepped
    ? points.map((point, i) => {
        const step = limits(steps[i].mean, steps[i].sigma);
        return { ...point, mean: step.mean, ucl3: step.ucl3, lcl3: step.lcl3, zone3: [step.lcl3, step.ucl3], zone2: [step.lcl2, step.ucl2], zone1: [step.lcl1, step.ucl1] };
      })
    : null;
  const sigma = center?.sigma ?? 0;
  const lastIndex = points.length - 1;
  const perStep = stepped ? " Limits are per recipe step, the same ones the rules check." : "";
  const caption = center
    ? `SPC chart of ${label}. Mean ${center.mean.toFixed(2)} ${unit}, sigma ${center.sigma.toFixed(2)}.${perStep} ${finding}`
    : `SPC chart of ${label}.${perStep} ${finding}`;
  const lowest = rows ? Math.min(...rows.map((row, i) => row.lcl3 - steps![i].sigma)) : band ? band.lcl3 - sigma : undefined;
  const highest = rows ? Math.max(...rows.map((row, i) => row.ucl3 + steps![i].sigma)) : band ? band.ucl3 + sigma : undefined;
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
          <XAxis dataKey="index" tick={{ fill: mpsColor.muted, fontSize: 10 }} stroke={mpsColor.line} />
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
            contentStyle={{ background: mpsColor.panel, border: `1px solid ${mpsColor.line}`, fontSize: 12 }}
            labelStyle={{ color: mpsColor.muted }}
          />
          {/* Each reference element must be a direct chart child: Recharts drops ones wrapped in a Fragment (#338). */}
          {constant ? <ReferenceArea y1={constant.lcl3} y2={constant.ucl3} fill={mpsColor.alarm} fillOpacity={0.08} /> : null}
          {constant ? <ReferenceArea y1={constant.lcl2} y2={constant.ucl2} fill={mpsColor.warn} fillOpacity={0.08} /> : null}
          {constant ? <ReferenceArea y1={constant.lcl1} y2={constant.ucl1} fill={mpsColor.nominal} fillOpacity={0.1} /> : null}
          {constant ? <ReferenceLine y={constant.ucl3} stroke={mpsColor.alarm} strokeDasharray="4 3" label={{ value: `UCL ${limitText(zone[2])}σ`, fill: mpsColor.alarm, fontSize: 10, position: "right" }} /> : null}
          {constant ? <ReferenceLine y={constant.lcl3} stroke={mpsColor.alarm} strokeDasharray="4 3" /> : null}
          {constant ? <ReferenceLine y={constant.mean} stroke={mpsColor.muted} label={{ value: "mean", fill: mpsColor.muted, fontSize: 10, position: "right" }} /> : null}
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
        {stepped ? <span data-limits="per-step">Limits follow the recipe step.</span> : null}
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
