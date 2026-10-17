// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company
// Chart bands for live series. Points, limits and rule results all come from the server; nothing here makes data up.

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
