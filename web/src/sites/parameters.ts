// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company
// Plain names for the comparison parameters. The wire value stays the contract id.

import type { ToolFamily } from "../api/types";

export const PARAMETER_LABEL: Record<string, string> = {
  ambientPressure: "Room pressure",
  arFlow: "Argon flow",
  ashRate: "Ash rate",
  bathTemperature: "Chemical bath temperature",
  cf4Flow: "CF4 flow",
  chamberPressure: "Chamber pressure",
  chf3Flow: "CHF3 flow",
  diwFlow: "DIW flow",
  edgeRingHeight: "Edge ring height",
  endpointTime: "Endpoint time",
  escTemperature: "ESC temperature",
  etchRate: "Etch rate",
  filmThickness: "Film thickness",
  filmUniformity: "Film uniformity",
  heaterTemperature: "Heater temperature",
  megasonicPower: "Megasonic power",
  n2h2Flow: "N2H2 flow",
  n2oFlow: "N2O flow",
  nh3Flow: "NH3 flow",
  o2Flow: "O2 flow",
  particlesAdders: "Particle adders",
  remotePlasmaPower: "Remote plasma power",
  residueCount: "Residue count",
  rfBiasPower: "Bias RF power",
  rfPower: "RF power",
  rfSourcePower: "Source RF power",
  sc1Flow: "SC1 flow",
  sc2Flow: "SC2 flow",
  sih4Flow: "SiH4 flow",
  spinChuckRunout: "Spin chuck runout",
  temperature: "Temperature",
  vibrationRms: "Vibration",
  waferCenterOffset: "Wafer center offset",
  wallTemperature: "Chamber wall temperature",
};

export const FAMILY_OPTIONS: { id: ToolFamily; label: string }[] = [
  { id: "deposition", label: "Thin-film deposition" },
  { id: "etch", label: "Plasma etch" },
  { id: "strip", label: "Photoresist strip" },
  { id: "clean", label: "Single-wafer clean" },
];

export function parameterLabel(parameter: string): string {
  return PARAMETER_LABEL[parameter] ?? parameter.replace(/([A-Z])/g, " $1").replace(/^./, (letter) => letter.toUpperCase());
}

/** Show the contract unit in words a reader already meets in the glossary. */
export function unitLabel(unit: string): string {
  if (unit === "C") return "°C";
  if (unit === "percent") return "%";
  return unit;
}
