// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company
import type { Investigation } from "./api/types";
import { CATEGORY_LABEL } from "./api/labels";

export type TraceKind = "rules" | "ai" | "human";

export type TraceRow = {
  id: string;
  kind: TraceKind;
  title: string;
  detail: string;
};

const DETERMINISTIC_SOURCES = new Set(["telemetry", "spc"]);

export function buildTrace(investigation: Investigation): TraceRow[] {
  const rows: TraceRow[] = [];
  const deterministic = investigation.evidence.filter((item) => DETERMINISTIC_SOURCES.has(item.source));

  if (deterministic.length === 0) {
    rows.push({
      id: `${investigation.id}-intake`,
      kind: "rules",
      title: "Evidence gate",
      detail: investigation.escalationReason ?? "No telemetry or SPC row is in this pack.",
    });
  } else {
    for (const item of deterministic) {
      rows.push({ id: item.id, kind: "rules", title: item.title, detail: item.excerpt });
    }
  }

  if (investigation.escalationReason && deterministic.length > 0) {
    rows.push({
      id: `${investigation.id}-escalation`,
      kind: "rules",
      title: "Evidence gate escalated",
      detail: investigation.escalationReason,
    });
  }

  for (const hypothesis of investigation.hypotheses) {
    rows.push({
      id: hypothesis.id,
      kind: "ai",
      title: CATEGORY_LABEL[hypothesis.category],
      detail: hypothesis.cause,
    });
  }

  if (
    investigation.stage === "awaitingApproval" ||
    investigation.stage === "approved" ||
    investigation.stage === "rejected" ||
    investigation.stage === "recommendationReady"
  ) {
    rows.push({
      id: `${investigation.id}-human`,
      kind: "human",
      title: "Human decision",
      detail:
        investigation.stage === "recommendationReady"
          ? "Waiting for an engineer to approve or reject. The model cannot write."
          : investigation.stage,
    });
  }

  return rows;
}
