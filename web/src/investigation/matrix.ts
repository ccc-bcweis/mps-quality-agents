// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company
import type { RootCauseCategory } from "../api/types";
import { S1_INVESTIGATION_ID, S2_INVESTIGATION_ID } from "../mocks/demoData";

export type MatrixMark = "supports" | "rulesOut" | "open";

export type MatrixRow = {
  id: string;
  label: string;
  note?: string;
  cells: Record<RootCauseCategory, MatrixMark>;
};

const CLASSES: RootCauseCategory[] = [
  "engineeringChange",
  "equipmentCalibrationOrAlignment",
  "supplyChainMaterial",
  "processDrift",
];

function row(id: string, label: string, marks: Partial<Record<RootCauseCategory, MatrixMark>>, note?: string): MatrixRow {
  const cells = Object.fromEntries(CLASSES.map((key) => [key, marks[key] ?? "open"])) as Record<RootCauseCategory, MatrixMark>;
  return { id, label, note, cells };
}

const S1: MatrixRow[] = [
  row("eco", "Edge ring change", { engineeringChange: "supports" }),
  row("mes", "Run record", { engineeringChange: "supports" }),
  row("bulletin", "Service note", { engineeringChange: "supports" }),
  row("oscillation", "Temperature oscillation", {
    equipmentCalibrationOrAlignment: "rulesOut",
    processDrift: "rulesOut",
  }),
  row("calibration", "Current calibration", { equipmentCalibrationOrAlignment: "rulesOut" }),
  row("maintenance", "Same-week maintenance", { equipmentCalibrationOrAlignment: "rulesOut" }),
  row("lot", "Shared gas lot", { supplyChainMaterial: "rulesOut" }),
];

const S2: MatrixRow[] = [
  row("plm", "Lifecycle revision", { engineeringChange: "supports" }),
  row("mes", "Run-header revision", { engineeringChange: "rulesOut" }),
  row("pressure", "Pressure step", {}, "Supports a real out-of-window signal. It does not name a cause."),
];

const BY_INVESTIGATION: Record<string, MatrixRow[]> = {
  [S1_INVESTIGATION_ID]: S1,
  [S2_INVESTIGATION_ID]: S2,
};

export const MATRIX_CLASSES: { id: RootCauseCategory; label: string }[] = [
  { id: "engineeringChange", label: "Engineering change" },
  { id: "equipmentCalibrationOrAlignment", label: "Equipment" },
  { id: "supplyChainMaterial", label: "Material" },
  { id: "processDrift", label: "Process drift" },
];

export function matrixFor(investigationId: string): MatrixRow[] {
  return BY_INVESTIGATION[investigationId] ?? [];
}

export function markLabel(mark: MatrixMark): string {
  if (mark === "supports") return "supports";
  if (mark === "rulesOut") return "rules out";
  return "—";
}
