// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company
import type { Investigation } from "../api/types";
import { Explain } from "./Explain";
import { Term } from "./Term";
import { formatStepDuration, oneSentence } from "../investigation/format";
import { buildAgentSteps, type StepStatus } from "../investigation/timeline";

const STATUS_CLASS: Record<StepStatus, string> = {
  queued: "border-mps-line text-mps-muted",
  running: "border-mps-ai bg-mps-ai text-mps-canvas",
  done: "border-mps-ok bg-mps-ok text-mps-canvas",
  escalated: "border-mps-warn bg-mps-warn text-mps-canvas",
};

const STATUS_WORD: Record<StepStatus, string> = {
  queued: "Waiting",
  running: "Running",
  done: "Done",
  escalated: "Escalated",
};

function StatusChip({ status }: { status: StepStatus }) {
  return (
    <span className={`rounded-pill border px-2 py-0.5 text-mps-micro font-semibold ${STATUS_CLASS[status]}`}>
      {STATUS_WORD[status]}
    </span>
  );
}

function Actor({ actor }: { actor: "rules" | "agent" | "human" }) {
  if (actor === "agent") return <Term id="AI" spellOut={false} />;
  if (actor === "human") return "Human";
  return "Rules";
}

export function AgentTimeline({ investigation }: { investigation: Investigation }) {
  const steps = buildAgentSteps(investigation);
  return (
    <div>
      <h2 className="mb-2 text-mps-title font-semibold">Agent steps</h2>
      <ol aria-label="Agent steps" className="m-0 list-none space-y-2 p-0">
        {steps.map((step) => (
          <li key={step.id} data-status={step.status} className="min-w-0 border-t border-mps-line pt-2">
            <div className="flex min-w-0 flex-wrap items-baseline gap-x-3 gap-y-1">
              <span className="font-mono text-mps-micro text-mps-muted">{step.n}</span>
              <span className="font-medium">{step.title}</span>
              <span className="text-mps-secondary">
                <Actor actor={step.actor} />
              </span>
              <span className="text-mps-muted">Duration {formatStepDuration(step.status, step.seconds)}</span>
              <StatusChip status={step.status} />
            </div>
            <p className="break-words text-mps-secondary">
              <Explain text={oneSentence(step.detail)} spellOut={false} />
            </p>
            <details className="mt-1">
              <summary className="cursor-pointer list-none text-mps-nominal [&::-webkit-details-marker]:hidden">Details</summary>
              <div className="mt-1 space-y-2 break-words text-mps-secondary">
                {step.detail !== oneSentence(step.detail) ? <p>{step.detail}</p> : null}
                {step.prompt ? (
                  <div>
                    <p className="font-medium text-mps-text">Prompt</p>
                    <pre className="mt-1 max-w-full overflow-x-auto whitespace-pre-wrap break-words font-mono text-mps-micro">{step.prompt}</pre>
                  </div>
                ) : (
                  <p>No model prompt. This step is rules or a person.</p>
                )}
                {step.payload ? (
                  <div>
                    <p className="font-medium text-mps-text">Tool payload</p>
                    <pre className="mt-1 max-w-full overflow-x-auto whitespace-pre-wrap break-words font-mono text-mps-micro">{step.payload}</pre>
                  </div>
                ) : null}
              </div>
            </details>
          </li>
        ))}
      </ol>
    </div>
  );
}
