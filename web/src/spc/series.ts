// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company
// Chart bands for live series. Points, limits and rule results all come from the server; nothing here makes data up.
import { limits as catalogLimits } from "../labels";

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

/** Chart zones from the server catalog (Western Electric rules 3, 2 and 1). NaN until the catalog has loaded. */
function zones(): [number, number, number] {
  const [inner, middle, outer] = catalogLimits()?.zoneSigmas ?? [];
  return [inner ?? Number.NaN, middle ?? Number.NaN, outer ?? Number.NaN];
}

/**
 * One point's limits from the recipe step it was read in. floor and ceiling are what the sensor can physically read
 * (from the fleet data); recipe, step and idle name the step for the dividers and the hover.
 */
export type StepLimit = {
  mean: number;
  sigma: number;
  floor?: number;
  ceiling?: number;
  recipe?: string;
  step?: string;
  idle?: boolean;
  /** Which segment (recipe step run) the point belongs to; a change is a step boundary. */
  segment?: number;
};

/** Keeps a limit inside what the sensor can physically read: no negative flow or power limits (#368). */
export function clampLimit(value: number, floor?: number, ceiling?: number): number {
  let result = value;
  if (floor !== undefined && Number.isFinite(floor)) result = Math.max(result, floor);
  if (ceiling !== undefined && Number.isFinite(ceiling)) result = Math.min(result, ceiling);
  return result;
}

/** Center plus or minus each zone's sigmas, clamped to the sensor's physical range when it is known. */
export function limits(mean: number, sigma: number, floor?: number, ceiling?: number): SpcLimits {
  const [inner, middle, outer] = zones();
  const clamp = (value: number) => clampLimit(value, floor, ceiling);
  return {
    mean,
    sigma,
    ucl1: clamp(mean + inner * sigma),
    lcl1: clamp(mean - inner * sigma),
    ucl2: clamp(mean + middle * sigma),
    lcl2: clamp(mean - middle * sigma),
    ucl3: clamp(mean + outer * sigma),
    lcl3: clamp(mean - outer * sigma),
  };
}
