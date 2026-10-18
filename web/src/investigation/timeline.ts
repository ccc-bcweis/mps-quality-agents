// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company
import type { Investigation, InvestigationStage } from "../api/types";

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
  seconds: number;
  prompt: string | null;
  payload: string | null;
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

const COPY: Record<StepId, Omit<AgentStep, "status" | "prompt" | "payload">> = {
  ticket: {
    id: "ticket",
    n: "01",
    title: "Alerts to ticket",
    actor: "rules",
    actorLabel: "Rule check",
    screen: "Alerts / Tickets",
    detail: "SPC and Western Electric open the alert and the ticket. This step is not an agent.",
    seconds: 1,
  },
  triage: {
    id: "triage",
    n: "02",
    title: "Triage",
    actor: "agent",
    actorLabel: "Investigation agent",
    screen: "Investigation",
    detail: "Plans the evidence pack. Does not diagnose.",
    seconds: 4,
  },
  hypotheses: {
    id: "hypotheses",
    n: "03",
    title: "Hypothesis generation",
    actor: "agent",
    actorLabel: "Investigation agent",
    screen: "Investigation",
    detail: "Four classes, none picked: engineering change, equipment calibration, material lot, and process drift.",
    seconds: 3,
  },
  toolCalls: {
    id: "toolCalls",
    n: "04",
    title: "Evidence tool calls",
    actor: "agent",
    actorLabel: "Investigation agent",
    screen: "Investigation",
    detail: "Read-only MES, QMS, PLM, ERP, CMMS, and documents. At most 10 calls.",
    seconds: 12,
  },
  gate: {
    id: "gate",
    n: "05",
    title: "Evidence gate",
    actor: "rules",
    actorLabel: "Rule check",
    screen: "Investigation",
    detail: "Checks that the pack is enough and that the records do not contradict each other.",
    seconds: 2,
  },
  escalation: {
    id: "escalation",
    n: "06",
    title: "Escalation path",
    actor: "rules",
    actorLabel: "Rule check",
    screen: "Investigation",
    detail: "Missing or contradictory evidence stops here. No root cause is asserted.",
    seconds: 1,
  },
  ranking: {
    id: "ranking",
    n: "07",
    title: "Hypothesis ranking",
    actor: "agent",
    actorLabel: "Resolution agent",
    screen: "Investigation",
    detail: "Orders the four classes with citations and the points that argue the other way.",
    seconds: 8,
  },
  capaDraft: {
    id: "capaDraft",
    n: "08",
    title: "CAPA draft",
    actor: "agent",
    actorLabel: "Resolution agent",
    screen: "Investigation",
    detail: "Drafts containment and a CAPA proposal. This is not a write.",
    seconds: 4,
  },
  grounding: {
    id: "grounding",
    n: "09",
    title: "Grounding check",
    actor: "rules",
    actorLabel: "Rule check",
    screen: "Investigation",
    detail: "Every cited id is in the pack. A claim with no citation does not pass.",
    seconds: 2,
  },
  approval: {
    id: "approval",
    n: "10",
    title: "Human approval",
    actor: "human",
    actorLabel: "Human",
    screen: "Approval",
    detail: "A person confirms. That mints a single-use approval token. Reject mints nothing.",
    seconds: 1,
  },
  qms: {
    id: "qms",
    n: "11",
    title: "CAPA in the quality system",
    actor: "rules",
    actorLabel: "Rule check",
    screen: "CAPA",
    detail: "The action service creates the CAPA after the token. Agents never write.",
    seconds: 1,
  },
};

const INVESTIGATION_PROMPT =
  "You are the investigation agent. Gather records with read-only tools. Do not diagnose, write, or approve.";

const RESOLUTION_PROMPT =
  "You are the resolution agent. Rank one cause from the records. You have no tools. Do not write or approve.";

function toolPayload(investigation: Investigation): string {
  return JSON.stringify(
    investigation.evidence.map((item) => ({
      source: item.source,
      ref: item.externalRef ?? null,
      at: item.observedAt,
      tool: item.toolId ?? null,
      text: item.excerpt,
      value: item.value ?? null,
      unit: item.unit ?? null,
      status: item.status ?? null,
    })),
    null,
    2,
  );
}

function rankPayload(investigation: Investigation): string {
  return JSON.stringify(
    investigation.hypotheses.map((item) => ({
      category: item.category,
      cause: item.cause,
      confidence: item.confidence,
      evidenceIds: item.evidenceIds,
      counterEvidence: item.counterEvidence,
      ruledOut: (item.ruledOut ?? []).map((entry) => ({
        evidenceId: entry.evidenceId,
        reason: entry.reason,
        source: entry.source,
        category: entry.category ?? null,
      })),
      actions: item.actions,
    })),
    null,
    2,
  );
}

function stepExtras(id: StepId, investigation: Investigation): { prompt: string | null; payload: string | null } {
  if (id === "triage" || id === "hypotheses") return { prompt: INVESTIGATION_PROMPT, payload: null };
  if (id === "toolCalls") return { prompt: INVESTIGATION_PROMPT, payload: toolPayload(investigation) };
  if (id === "ranking" || id === "capaDraft") return { prompt: RESOLUTION_PROMPT, payload: rankPayload(investigation) };
  if (id === "gate" || id === "escalation" || id === "grounding") return { prompt: null, payload: toolPayload(investigation) };
  return { prompt: null, payload: null };
}

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
    const extras = stepExtras(id, investigation);
    const step: AgentStep = { ...COPY[id], status: status[id], prompt: extras.prompt, payload: extras.payload };
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
