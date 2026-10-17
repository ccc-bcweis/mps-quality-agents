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

export function limits(mean: number, sigma: number): SpcLimits {
  const [inner, middle, outer] = zones();
  return {
    mean,
    sigma,
    ucl1: mean + inner * sigma,
    lcl1: mean - inner * sigma,
    ucl2: mean + middle * sigma,
    lcl2: mean - middle * sigma,
    ucl3: mean + outer * sigma,
    lcl3: mean - outer * sigma,
  };
}
