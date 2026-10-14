// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company
// PLAN §6.5 inputs. Shares of the COPQ remainder and the downtime-hours lever
// are stated assumptions: the plan names the buckets and does not publish percents.

export type RoiAssumptions = {
  revenueUsd: number;
  copqRate: Fraction;
  incidentsPerYear: number;
  mttrDays: number;
  hypothesisDays: number;
  repeatDefectRate: Fraction;
  capaCycleDays: number;
  engineerHourlyUsd: number;
  hoursPerInvestigation: number;
  downtimeHourlyUsd: number;
  scrapShare: { numerator: number; denominator: number };
  warrantyShare: { numerator: number; denominator: number };
  downtimeShare: { numerator: number; denominator: number };
  investigationHours: { conservative: Fraction; expected: Fraction };
  hypothesisTime: { conservative: Fraction; expected: Fraction };
  repeatDefects: { conservative: Fraction; expected: Fraction };
  capaCycle: { conservative: Fraction; expected: Fraction };
  downtimeAvoided: { conservative: Fraction; expected: Fraction };
  aiCostPerInvestigation: Fraction;
};

export type Fraction = { numerator: number; denominator: number };

export const roiAssumptions: RoiAssumptions = {
  revenueUsd: 1_200_000_000,
  copqRate: { numerator: 8, denominator: 100 },
  incidentsPerYear: 1400,
  mttrDays: 9.5,
  hypothesisDays: 4,
  repeatDefectRate: { numerator: 18, denominator: 100 },
  capaCycleDays: 62,
  engineerHourlyUsd: 95,
  hoursPerInvestigation: 22,
  downtimeHourlyUsd: 12_000,
  scrapShare: { numerator: 1, denominator: 2 },
  warrantyShare: { numerator: 1, denominator: 5 },
  downtimeShare: { numerator: 3, denominator: 10 },
  investigationHours: {
    conservative: { numerator: 3, denominator: 10 },
    expected: { numerator: 45, denominator: 100 },
  },
  hypothesisTime: {
    conservative: { numerator: 1, denominator: 2 },
    expected: { numerator: 7, denominator: 10 },
  },
  repeatDefects: {
    conservative: { numerator: 15, denominator: 100 },
    expected: { numerator: 1, denominator: 4 },
  },
  capaCycle: {
    conservative: { numerator: 1, denominator: 5 },
    expected: { numerator: 35, denominator: 100 },
  },
  downtimeAvoided: {
    conservative: { numerator: 8, denominator: 100 },
    expected: { numerator: 15, denominator: 100 },
  },
  aiCostPerInvestigation: { numerator: 6, denominator: 100 },
};

function mul(amount: number, fraction: Fraction): number {
  return (amount * fraction.numerator) / fraction.denominator;
}

export type RoiProjection = {
  copqUsd: number;
  laborUsd: number;
  scrapUsd: number;
  warrantyUsd: number;
  downtimeUsd: number;
  downtimeHours: number;
  repeatScrapUsd: number;
  conservativeSavingsUsd: number;
  expectedSavingsUsd: number;
  hypothesisDaysExpected: number;
  capaDaysExpected: number;
  modelRunCostUsd: number;
};

export function projectRoi(input: RoiAssumptions = roiAssumptions): RoiProjection {
  const copqUsd = mul(input.revenueUsd, input.copqRate);
  const laborUsd = input.incidentsPerYear * input.hoursPerInvestigation * input.engineerHourlyUsd;
  const remainder = copqUsd - laborUsd;
  const scrapUsd = mul(remainder, input.scrapShare);
  const warrantyUsd = mul(remainder, input.warrantyShare);
  const downtimeUsd = mul(remainder, input.downtimeShare);
  const repeatScrapUsd = mul(scrapUsd, input.repeatDefectRate);
  const savings = (hours: Fraction, repeat: Fraction, down: Fraction) =>
    mul(laborUsd, hours) + mul(repeatScrapUsd, repeat) + mul(downtimeUsd, down);

  return {
    copqUsd,
    laborUsd,
    scrapUsd,
    warrantyUsd,
    downtimeUsd,
    downtimeHours: downtimeUsd / input.downtimeHourlyUsd,
    repeatScrapUsd,
    conservativeSavingsUsd: savings(
      input.investigationHours.conservative,
      input.repeatDefects.conservative,
      input.downtimeAvoided.conservative,
    ),
    expectedSavingsUsd: savings(
      input.investigationHours.expected,
      input.repeatDefects.expected,
      input.downtimeAvoided.expected,
    ),
    hypothesisDaysExpected: input.hypothesisDays * (1 - input.hypothesisTime.expected.numerator / input.hypothesisTime.expected.denominator),
    capaDaysExpected: input.capaCycleDays * (1 - input.capaCycle.expected.numerator / input.capaCycle.expected.denominator),
    modelRunCostUsd: mul(input.incidentsPerYear, input.aiCostPerInvestigation),
  };
}
