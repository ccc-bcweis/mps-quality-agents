// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company
import { useState } from "react";
import type { Investigation } from "../api/types";
import { HarnessPrintout } from "./HarnessPrintout";
import { btn, btnInfo } from "./ui";

/** Matches InvestigationRounds.MaxEvidenceLength. */
export const MAX_EVIDENCE_LENGTH = 4000;
/** Matches EvidenceRoundSettings.DefaultMaxRounds when an older record has no maxRounds. */
export const DEFAULT_MAX_ROUNDS = 5;

export function roundOf(investigation: Investigation): number {
  return investigation.round && investigation.round > 0 ? investigation.round : 1;
}

export function maxRoundsOf(investigation: Investigation): number {
  return investigation.maxRounds && investigation.maxRounds > 0 ? investigation.maxRounds : DEFAULT_MAX_ROUNDS;
}

/** True while another round is allowed on this ticket. */
export function canAddRound(investigation: Investigation): boolean {
  return roundOf(investigation) < maxRoundsOf(investigation);
}

/**
 * Add evidence: free text, and optionally a bench printout through the manual upload. Submitting closes this round
 * with the evidence on file, and the agents run the next round from hypothesis generation.
 */
export function AddEvidenceBox({
  investigation,
  pending,
  onSubmit,
  onCancel,
}: {
  investigation: Investigation;
  pending: boolean;
  onSubmit: (text: string) => void;
  onCancel?: () => void;
}) {
  const [text, setText] = useState("");
  const round = roundOf(investigation);
  const max = maxRoundsOf(investigation);
  if (!canAddRound(investigation)) {
    return (
      <div className="mt-4 rounded border border-mps-line p-3" data-testid="add-evidence-cap">
        <h3 className="font-semibold">Add evidence</h3>
        <p className="mt-1 text-mps-secondary">
          This ticket has reached {max} rounds, the most allowed. Approve, override or reject this round.
        </p>
      </div>
    );
  }

  const trimmed = text.trim();
  return (
    <div className="mt-4 rounded border border-mps-line p-3" data-testid="add-evidence">
      <h3 className="font-semibold">Add evidence for round {round + 1}</h3>
      <p className="mt-1 text-mps-secondary">
        Write what you know that the agents do not: what you saw on the tool, a reading, a recent change. It is saved with your name and the time, marked as added by a person, and the agents run again with it as round {round + 1} of at most {max}.
      </p>
      <label className="mt-2 block">
        <span className="text-mps-secondary">Evidence to add</span>
        <textarea
          aria-label="Evidence to add"
          className="mt-1 block w-full rounded border border-mps-line bg-transparent p-2"
          rows={4}
          maxLength={MAX_EVIDENCE_LENGTH}
          value={text}
          onChange={(event) => setText(event.target.value)}
        />
      </label>
      <div className="mt-2">
        <HarnessPrintout
          tone="panel"
          label="Attach a bench printout (optional)"
          onConfirmed={(summary) => setText((current) => (current.trim() ? `${current.trim()}\n${summary}` : summary))}
        />
      </div>
      <div className="mt-3 flex flex-wrap gap-2">
        <button type="button" className={btnInfo} disabled={pending || trimmed.length === 0} onClick={() => onSubmit(trimmed)}>
          {pending ? "Adding…" : `Add evidence and run round ${round + 1}`}
        </button>
        {onCancel ? (
          <button type="button" className={btn} disabled={pending} onClick={onCancel}>
            Cancel
          </button>
        ) : null}
      </div>
      {trimmed.length === 0 ? <p className="mt-2 font-mono text-mps-micro text-mps-muted">Type the evidence to add. The agents read it in the next round.</p> : null}
    </div>
  );
}
