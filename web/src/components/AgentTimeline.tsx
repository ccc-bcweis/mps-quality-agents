// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company
import type { Investigation } from "../api/types";
import { buildAgentSteps, type StepStatus } from "../investigation/timeline";

const STATUS_CLASS: Record<StepStatus, string> = {
  queued: "border-mps-line text-mps-muted",
  running: "border-mps-ai bg-mps-ai text-mps-canvas",
  done: "border-mps-ok bg-mps-ok text-mps-canvas",
  escalated: "border-mps-warn bg-mps-warn text-mps-canvas",
};

const ACTOR_CLASS = {
  rules: "text-mps-nominal",
  agent: "text-mps-ai",
  human: "text-mps-warn",
} as const;

function StatusChip({ status }: { status: StepStatus }) {
  return (
    <span className={`rounded-pill border px-2 py-0.5 font-mono text-mps-micro font-semibold ${STATUS_CLASS[status]}`}>
      {status}
    </span>
  );
}

export function AgentTimeline({ investigation }: { investigation: Investigation }) {
  const steps = buildAgentSteps(investigation);
  return (
    <div>
      <h2 className="mb-2 text-mps-title font-semibold">Agent steps</h2>
      <p className="mb-2 flex flex-wrap items-center gap-2 text-mps-secondary">
        Status chips
        <StatusChip status="queued" />
        <StatusChip status="running" />
        <StatusChip status="done" />
        <StatusChip status="escalated" />
      </p>
      <ol aria-label="Agent steps" className="space-y-2">
        {steps.map((step) => (
          <li key={step.id} data-status={step.status} className="flex gap-3 border-t border-mps-line pt-2">
            <span className="font-mono text-mps-micro text-mps-muted">{step.n}</span>
            <div>
              <div className="flex flex-wrap items-center gap-2">
                <span className="font-medium">{step.title}</span>
                <StatusChip status={step.status} />
              </div>
              <p className={`font-mono text-mps-micro uppercase ${ACTOR_CLASS[step.actor]}`}>
                {step.actorLabel} · {step.screen}
              </p>
              <p className="text-mps-secondary">{step.detail}</p>
            </div>
          </li>
        ))}
      </ol>
    </div>
  );
}
