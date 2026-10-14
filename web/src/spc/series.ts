// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company
// Client-side series for the console charts. The alert service remains the authority.

export type SeriesShape = "nominal" | "oscillation" | "step";

export type SpcPoint = { index: number; value: number };

export type SpcLimits = {
  mean: number;
  sigma: number;
  ucl1: number;
  lcl1: number;
  ucl2: number;
  lcl2: number;
  ucl3: number;
  lcl3: number;
};

export function limits(mean: number, sigma: number): SpcLimits {
  return {
    mean,
    sigma,
    ucl1: mean + sigma,
    lcl1: mean - sigma,
    ucl2: mean + 2 * sigma,
    lcl2: mean - 2 * sigma,
    ucl3: mean + 3 * sigma,
    lcl3: mean - 3 * sigma,
  };
}

export function seedFrom(text: string): number {
  let hash = 2166136261;
  for (let index = 0; index < text.length; index += 1) {
    hash ^= text.charCodeAt(index);
    hash = Math.imul(hash, 16777619);
  }
  return hash >>> 0;
}

function mulberry32(seed: number): () => number {
  let state = seed >>> 0;
  return () => {
    state = (state + 0x6d2b79f5) | 0;
    let value = Math.imul(state ^ (state >>> 15), 1 | state);
    value = (value + Math.imul(value ^ (value >>> 7), 61 | value)) ^ value;
    return ((value ^ (value >>> 14)) >>> 0) / 4294967296;
  };
}

export function buildSeries(options: {
  seed: number;
  mean: number;
  sigma: number;
  shape: SeriesShape;
  count?: number;
  endValue?: number;
}): SpcPoint[] {
  const count = options.count ?? 60;
  const random = mulberry32(options.seed);
  const onset = Math.floor(count * 0.7);
  const points: SpcPoint[] = [];
  for (let index = 0; index < count; index += 1) {
    const noise = (random() - 0.5) * 2 * options.sigma * 0.35;
    let value = options.mean + noise;
    if (options.shape === "oscillation" && index >= onset) {
      value += Math.sin((index - onset) * 0.85) * options.sigma * 2.6;
    }
    if (options.shape === "step" && index >= onset) {
      value += options.sigma * 3.6;
    }
    points.push({ index, value });
  }
  if (options.endValue !== undefined && points.length > 0) {
    points[points.length - 1] = { index: count - 1, value: options.endValue };
  }
  return points;
}

export function violatesWesternElectric1(values: number[], mean: number, sigma: number): boolean {
  return values.some((value) => Math.abs(value - mean) > 3 * sigma);
}

export function violatesWesternElectric2(values: number[], mean: number, sigma: number): boolean {
  const band = 2 * sigma;
  for (let index = 2; index < values.length; index += 1) {
    const window = [values[index - 2], values[index - 1], values[index]];
    const above = window.filter((value) => value > mean + band).length;
    const below = window.filter((value) => value < mean - band).length;
    if (above >= 2 || below >= 2) return true;
  }
  return false;
}
