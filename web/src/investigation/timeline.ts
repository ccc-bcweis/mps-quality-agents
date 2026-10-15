// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company
import type { Investigation, InvestigationStage } from "../api/types";

export const INVESTIGATION_POLL_MS = 5000;

export type StepStatus = "queued" | "running" | "done" | "escalated";
export type StepActor = "rules" | "agent" | "human";

export type AgentStep = {
  id: string;
  n: string;
  title: string;
  actor: StepActor;
  actorLabel: string;
  screen: string;
  detail: string;
  status: StepStatus;
};

const ORDER = [
  "ticket",
  "triage",
  "hypotheses",
  "toolCalls",
  "gate",
  "escalation",
  "ranking",
  "capaDraft",
  "grounding",
  "approval",
  "qms",
] as const;

type StepId = (typeof ORDER)[number];

const COPY: Record<StepId, Omit<AgentStep, "status">> = {
  ticket: {
    id: "ticket",
    n: "01",
    title: "Alerts to ticket",
    actor: "rules",
    actorLabel: "Deterministic",
    screen: "Alerts / Tickets",
    detail: "SPC and Western Electric open the alert and the ticket. This step is not an agent.",
  },
  triage: {
    id: "triage",
    n: "02",
    title: "Triage",
    actor: "agent",
    actorLabel: "Investigation agent",
    screen: "Investigation",
    detail: "Plans the evidence pack. Does not diagnose.",
  },
  hypotheses: {
    id: "hypotheses",
    n: "03",
    title: "Hypothesis generation",
    actor: "agent",
    actorLabel: "Investigation agent",
    screen: "Investigation",
    detail: "Four classes, none picked: engineering change, equipment calibration, material lot, and process drift.",
  },
  toolCalls: {
    id: "toolCalls",
    n: "04",
    title: "Evidence tool calls",
    actor: "agent",
    actorLabel: "Investigation agent",
    screen: "Investigation",
    detail: "Read-only MES, QMS, PLM, ERP, CMMS, and documents. At most 10 calls.",
  },
  gate: {
    id: "gate",
    n: "05",
    title: "Evidence gate",
    actor: "rules",
    actorLabel: "Deterministic",
    screen: "Investigation",
    detail: "Checks that the pack is enough and that the records do not contradict each other.",
  },
  escalation: {
    id: "escalation",
    n: "06",
    title: "Escalation path",
    actor: "rules",
    actorLabel: "Deterministic",
    screen: "Investigation",
    detail: "Missing or contradictory evidence stops here. No root cause is asserted.",
  },
  ranking: {
    id: "ranking",
    n: "07",
    title: "Hypothesis ranking",
    actor: "agent",
    actorLabel: "Resolution agent",
    screen: "Investigation",
    detail: "Orders the four classes with citations and the points that argue the other way.",
  },
  capaDraft: {
    id: "capaDraft",
    n: "08",
    title: "CAPA draft",
    actor: "agent",
    actorLabel: "Resolution agent",
    screen: "Investigation",
    detail: "Drafts containment and a CAPA proposal. This is not a write.",
  },
  grounding: {
    id: "grounding",
    n: "09",
    title: "Grounding check",
    actor: "rules",
    actorLabel: "Deterministic",
    screen: "Investigation",
    detail: "Every cited id is in the pack. A claim with no citation does not pass.",
  },
  approval: {
    id: "approval",
    n: "10",
    title: "Human approval",
    actor: "human",
    actorLabel: "Human",
    screen: "Approval",
    detail: "A person confirms. That mints a single-use approval token. Reject mints nothing.",
  },
  qms: {
    id: "qms",
    n: "11",
    title: "CAPA in the quality system",
    actor: "rules",
    actorLabel: "Deterministic",
    screen: "CAPA",
    detail: "The action service creates the CAPA after the token. Agents never write.",
  },
};

function statuses(stage: InvestigationStage): Record<StepId, StepStatus> {
  const queued = Object.fromEntries(ORDER.map((id) => [id, "queued"])) as Record<StepId, StepStatus>;
  const early: Record<StepId, StepStatus> = { ...queued, ticket: "done" };
  if (stage === "started") return { ...early, triage: "running" };
  if (stage === "evidenceGathered") {
    return { ...early, triage: "done", hypotheses: "done", toolCalls: "done", gate: "running" };
  }
  if (stage === "escalated" || stage === "failed") {
    return {
      ...early,
      triage: "done",
      hypotheses: "done",
      toolCalls: "done",
      gate: "escalated",
      escalation: "escalated",
    };
  }
  const ready: Record<StepId, StepStatus> = {
    ...early,
    triage: "done",
    hypotheses: "done",
    toolCalls: "done",
    gate: "done",
    escalation: "done",
    ranking: "done",
    capaDraft: "done",
    grounding: "done",
    approval: "queued",
    qms: "queued",
  };
  if (stage === "awaitingApproval") return { ...ready, approval: "running" };
  if (stage === "approved") return { ...ready, approval: "done", qms: "done" };
  if (stage === "rejected") return { ...ready, approval: "done" };
  return ready;
}

export function buildAgentSteps(investigation: Investigation): AgentStep[] {
  const status = statuses(investigation.stage);
  return ORDER.map((id) => {
    const step = { ...COPY[id], status: status[id] };
    if (id === "toolCalls") {
      const count = Math.min(10, investigation.evidence.length);
      step.detail = `${step.detail} This pack used ${count}.`;
    }
    if (id === "gate" && investigation.escalationReason) {
      step.detail = "The gate stopped. The records disagree or a required record is missing.";
    }
    if (id === "escalation" && !investigation.escalationReason && status.escalation === "done") {
      step.detail = "Checked. Not taken. The pack was sufficient.";
    }
    return step;
  });
}
