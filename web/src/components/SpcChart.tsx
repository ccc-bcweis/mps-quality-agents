// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company
import { CartesianGrid, ComposedChart, Line, ReferenceArea, ReferenceLine, Tooltip, XAxis, YAxis } from "recharts";
import { mpsColor } from "../brand/colors";
import { Term } from "./Term";
import { bandFromEarly, buildSeries, limits, seedFrom, violatesWesternElectric1, violatesWesternElectric2, type SeriesShape, type SpcPoint } from "../spc/series";

export function SpcChart({
  seedKey,
  label,
  unit,
  mean,
  sigma,
  shape,
  endValue,
  points: livePoints,
}: {
  seedKey: string;
  label: string;
  unit: string;
  mean: number;
  sigma: number;
  shape: SeriesShape;
  endValue?: number;
  /** Live samples from the plant tick. When absent, the chart draws the frozen teaching series. */
  points?: SpcPoint[];
}) {
  const points = livePoints && livePoints.length > 0 ? livePoints : buildSeries({ seed: seedFrom(seedKey), mean, sigma, shape, endValue });
  const values = points.map((point) => point.value);
  const center = livePoints && livePoints.length > 0 ? bandFromEarly(values, mean, sigma) : { mean, sigma };
  const band = limits(center.mean, center.sigma);
  const rule1 = violatesWesternElectric1(values, center.mean, center.sigma);
  const rule2 = violatesWesternElectric2(values, center.mean, center.sigma);
  const finding = rule1
    ? "Western Electric rule 1: a point is beyond 3 sigma."
    : rule2
      ? "Western Electric rule 2: two of three points are beyond 2 sigma."
      : "Western Electric rules 1 and 2 are clear on this series.";
  const caption = `SPC chart of ${label}. Mean ${center.mean.toFixed(2)} ${unit}, sigma ${center.sigma.toFixed(2)}. ${finding} Client check on the drawn series.`;

  return (
    <figure aria-label={caption} className="overflow-x-auto">
      <div aria-hidden="true">
        <ComposedChart width={720} height={220} data={points} margin={{ top: 16, right: 56, bottom: 8, left: 8 }}>
          <CartesianGrid stroke={mpsColor.line} strokeDasharray="3 3" />
          <XAxis dataKey="index" tick={{ fill: mpsColor.muted, fontSize: 10 }} stroke={mpsColor.line} />
          <YAxis
            domain={[Math.min(band.lcl3 - center.sigma, ...values), Math.max(band.ucl3 + center.sigma, ...values)]}
            tick={{ fill: mpsColor.muted, fontSize: 10 }}
            stroke={mpsColor.line}
            width={48}
          />
          <Tooltip
            contentStyle={{ background: mpsColor.panel, border: `1px solid ${mpsColor.line}`, fontSize: 12 }}
            labelStyle={{ color: mpsColor.muted }}
          />
          <ReferenceArea y1={band.lcl3} y2={band.ucl3} fill={mpsColor.alarm} fillOpacity={0.08} />
          <ReferenceArea y1={band.lcl2} y2={band.ucl2} fill={mpsColor.warn} fillOpacity={0.08} />
          <ReferenceArea y1={band.lcl1} y2={band.ucl1} fill={mpsColor.nominal} fillOpacity={0.1} />
          <ReferenceLine y={band.ucl3} stroke={mpsColor.alarm} strokeDasharray="4 3" label={{ value: "UCL 3σ", fill: mpsColor.alarm, fontSize: 10, position: "right" }} />
          <ReferenceLine y={band.lcl3} stroke={mpsColor.alarm} strokeDasharray="4 3" />
          <ReferenceLine y={band.mean} stroke={mpsColor.muted} label={{ value: "mean", fill: mpsColor.muted, fontSize: 10, position: "right" }} />
          <Line type="monotone" dataKey="value" stroke={mpsColor.secondary} strokeWidth={1.4} dot={false} isAnimationActive={false} />
        </ComposedChart>
      </div>
      <figcaption className="mt-2 flex flex-wrap gap-3 text-mps-micro text-mps-muted">
        <span>σ1 <i className="inline-block h-2 w-3 bg-mps-nominal" /></span>
        <span>σ2 <i className="inline-block h-2 w-3 bg-mps-warn" /></span>
        <span>σ3 <i className="inline-block h-2 w-3 bg-mps-alarm" /></span>
        <span className="text-mps-secondary">{finding}</span>
        <span>
          <Term id="UCL" spellOut={false} /> and <Term id="LCL" spellOut={false} /> are the outer chart lines.
        </span>
      </figcaption>
    </figure>
  );
}
