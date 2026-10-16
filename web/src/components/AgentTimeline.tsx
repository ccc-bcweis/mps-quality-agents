// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company
import type { Investigation } from "../api/types";
import { Explain } from "./Explain";
import { Term } from "./Term";
import { formatStepDuration, oneSentence } from "../investigation/format";
import { useState } from "react";
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
          <AgentStepRow key={step.id} step={step} />
        ))}
      </ol>
    </div>
  );
}

type Step = ReturnType<typeof buildAgentSteps>[number];

/** One step. The whole row toggles the detail; the chevron on the left shows whether it is open. */
function AgentStepRow({ step }: { step: Step }) {
  const [open, setOpen] = useState(false);
  const panelId = `step-detail-${step.id}`;
  return (
    <li data-status={step.status} className="min-w-0 border-t border-mps-line pt-2">
      <button
        type="button"
        aria-expanded={open}
        aria-controls={panelId}
        onClick={() => setOpen((value) => !value)}
        className="flex w-full min-w-0 items-start gap-2 rounded bg-transparent p-0 text-left text-mps-text hover:bg-mps-raised focus-visible:outline focus-visible:outline-2 focus-visible:outline-mps-nominal"
      >
        <span aria-hidden="true" className={`mt-0.5 inline-block w-4 shrink-0 text-mps-muted transition-transform ${open ? "rotate-90" : ""}`}>
          ▶
        </span>
        <span className="min-w-0 flex-1">
          <span className="flex min-w-0 flex-wrap items-baseline gap-x-3 gap-y-1">
            <span className="font-mono text-mps-micro text-mps-muted">{step.n}</span>
            <span className="font-medium">{step.title}</span>
            <span className="text-mps-secondary">
              <Actor actor={step.actor} />
            </span>
            <span className="text-mps-muted">Duration {formatStepDuration(step.status, step.seconds)}</span>
            <StatusChip status={step.status} />
          </span>
          <span className="block break-words text-mps-secondary">
            <Explain text={oneSentence(step.detail)} spellOut={false} />
          </span>
        </span>
      </button>
      {open ? (
        <div id={panelId} className="ml-6 mt-1 space-y-2 break-words text-mps-secondary">
          {step.detail !== oneSentence(step.detail) ? <p>{step.detail}</p> : null}
          {step.prompt ? (
            <div>
              <p className="font-medium text-mps-text">Prompt</p>
              <pre className="mt-1 max-w-full overflow-x-auto whitespace-pre-wrap break-words font-mono text-mps-micro">{step.prompt}</pre>
            </div>
          ) : (
            <p>No AI prompt. This step is a rule check or a person.</p>
          )}
          {step.payload ? (
            <div>
              <p className="font-medium text-mps-text">Tool payload</p>
              <pre className="mt-1 max-w-full overflow-x-auto whitespace-pre-wrap break-words font-mono text-mps-micro">{step.payload}</pre>
            </div>
          ) : null}
        </div>
      ) : null}
    </li>
  );
}
