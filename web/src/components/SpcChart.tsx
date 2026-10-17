// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company
import { CartesianGrid, ComposedChart, Line, ReferenceArea, ReferenceDot, ReferenceLine, ResponsiveContainer, Tooltip, XAxis, YAxis } from "recharts";
import type { ManualMarker } from "../live/manual";
import { mpsColor } from "../brand/colors";
import { Term } from "./Term";
import { limits, type SpcPoint } from "../spc/series";
import { WAITING_FOR_READINGS } from "../fleet/catalog";

export function SpcChart({
  label,
  unit,
  center,
  points: livePoints,
  finding,
  cumulative = false,
  manual,
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
  const sigma = center?.sigma ?? 0;
  const caption = center
    ? `SPC chart of ${label}. Mean ${center.mean.toFixed(2)} ${unit}, sigma ${center.sigma.toFixed(2)}. ${finding}`
    : `SPC chart of ${label}. ${finding}`;

  return (
    <figure aria-label={caption} className="w-full">
      <div aria-hidden="true" className="h-[240px] w-full">
        <ResponsiveContainer width="100%" height={240} initialDimension={{ width: 720, height: 240 }}>
        <ComposedChart data={points} margin={{ top: 16, right: 56, bottom: 8, left: 8 }}>
          <CartesianGrid stroke={mpsColor.line} strokeDasharray="3 3" />
          <XAxis dataKey="index" tick={{ fill: mpsColor.muted, fontSize: 10 }} stroke={mpsColor.line} />
          <YAxis
            domain={[
              Math.min(...(band ? [band.lcl3 - sigma] : []), ...values, ...(manual ? [manual.value] : [])),
              Math.max(...(band ? [band.ucl3 + sigma] : []), ...values, ...(manual ? [manual.value] : [])),
            ]}
            tick={{ fill: mpsColor.muted, fontSize: 10 }}
            tickFormatter={(value: number) => axisNumber(value, sigma)}
            tickCount={6}
            stroke={mpsColor.line}
            width={64}
            label={{ value: unit, angle: -90, position: "insideLeft", fill: mpsColor.muted, fontSize: 10 }}
          />
          <Tooltip
            formatter={(value: number) => `${axisNumber(value, sigma)} ${unit}`}
            contentStyle={{ background: mpsColor.panel, border: `1px solid ${mpsColor.line}`, fontSize: 12 }}
            labelStyle={{ color: mpsColor.muted }}
          />
          {band ? (
            <>
              <ReferenceArea y1={band.lcl3} y2={band.ucl3} fill={mpsColor.alarm} fillOpacity={0.08} />
              <ReferenceArea y1={band.lcl2} y2={band.ucl2} fill={mpsColor.warn} fillOpacity={0.08} />
              <ReferenceArea y1={band.lcl1} y2={band.ucl1} fill={mpsColor.nominal} fillOpacity={0.1} />
              <ReferenceLine y={band.ucl3} stroke={mpsColor.alarm} strokeDasharray="4 3" label={{ value: "UCL 3σ", fill: mpsColor.alarm, fontSize: 10, position: "right" }} />
              <ReferenceLine y={band.lcl3} stroke={mpsColor.alarm} strokeDasharray="4 3" />
              <ReferenceLine y={band.mean} stroke={mpsColor.muted} label={{ value: "mean", fill: mpsColor.muted, fontSize: 10, position: "right" }} />
            </>
          ) : null}
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
        <span>σ1 <i className="inline-block h-2 w-3 bg-mps-nominal" /></span>
        <span>σ2 <i className="inline-block h-2 w-3 bg-mps-warn" /></span>
        <span>σ3 <i className="inline-block h-2 w-3 bg-mps-alarm" /></span>
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
