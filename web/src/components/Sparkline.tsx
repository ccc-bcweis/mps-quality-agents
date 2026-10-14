// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company
import { mpsColor } from "../brand/colors";

export function Sparkline({ values, color = mpsColor.nominal }: { values: number[]; color?: string }) {
  const width = 220;
  const height = 28;
  const min = Math.min(...values);
  const max = Math.max(...values);
  const span = max - min || 1;
  const coords = values
    .map((value, index) => {
      const x = (index / (values.length - 1)) * width;
      const y = height - 4 - ((value - min) / span) * (height - 8);
      return `${x.toFixed(1)},${y.toFixed(1)}`;
    })
    .join(" ");
  const last = coords.split(" ").at(-1)?.split(",") ?? ["0", "0"];

  return (
    <svg className="mt-2 h-7 w-full" viewBox={`0 0 ${width} ${height}`} preserveAspectRatio="none" aria-hidden="true">
      <polyline fill="none" stroke={color} strokeWidth="1.5" points={coords} />
      <circle cx={last[0]} cy={last[1]} r="2.2" fill={color} />
    </svg>
  );
}
