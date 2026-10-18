// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company
import { useEffect, useState } from "react";
import { useNavigate } from "react-router-dom";
import { api, ApiProblem } from "../api/client";
import { CATEGORY_LABEL, INVESTIGATION_STAGE_LABEL, limitText, limits } from "../labels";
import type { DecisionRecord, HumanDecisionKind, Investigation } from "../api/types";
import { ApprovalDialog, type DecisionMode } from "./ApprovalDialog";
import { AddEvidenceBox, canAddRound, maxRoundsOf, roundOf } from "./AddEvidenceBox";
import { EvidenceAgainst } from "./RuledOutRecords";
import { Explain } from "./Explain";
import { Term } from "./Term";
import { btnDanger, btnInfo, btnOk, panel } from "./ui";
import { plainFinding } from "../investigation/format";
import { useConsole } from "../context/ConsoleContext";
import { visibleProblem } from "../history/copy";

/** Matches HumanDecisionService.MinOverrideJustification (ADR-005). */

/**
 * The human decision for the current round: approve, reject, add evidence (request more evidence), or override an
 * escalation. A decision is final per round; adding evidence closes this round and the agents run the next one.
 */
export function DecisionPanel({ investigation, onChange }: { investigation: Investigation; onChange: (next: Investigation) => void }) {
  const navigate = useNavigate();
  const { rememberCapa } = useConsole();
  const [mode, setMode] = useState<DecisionMode | null>(null);
  const [approverId, setApproverId] = useState("");
  const [pending, setPending] = useState(false);
  const [note, setNote] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [adding, setAdding] = useState(false);
  useEffect(() => {
    let cancelled = false;
    api
      .getAdminSession()
      .then((next) => {
        if (!cancelled) setApproverId(next.displayName);
      })
      .catch(() => undefined);
    return () => {
      cancelled = true;
    };
  }, []);

  async function decide(kind: HumanDecisionKind, comment?: string) {
    setPending(true);
    setError(null);
    try {
      const result = await api.submitDecision(investigation.id, {
        kind,
        approverId: approverId.trim() || undefined,
        comment: comment ?? null,
      });
      onChange(result.investigation);
      setMode(null);
      setAdding(false);
      if (kind === "requestInfo" && result.investigation.id !== investigation.id) {
        navigate(`/investigations/${result.investigation.id}`);
        return;
      }
      if (result.capa) {
        rememberCapa(result.capa);
        navigate(`/capas/${result.capa.id}`);
      }
    } catch (reason: unknown) {
      setError(
        reason instanceof ApiProblem ? (reason.status === 409 && reason.detail ? reason.detail : reason.title) : "The decision was not accepted.",
      );
    } finally {
      setPending(false);
    }
  }

  const problem = visibleProblem(error);
  const escalated = investigation.stage === "escalated";
  const justified = note.trim().length >= (limits()?.overrideMinChars ?? Number.POSITIVE_INFINITY);
  const top = investigation.hypotheses.slice().sort((a, b) => b.confidence - a.confidence)[0];
  const record = recordedDecision(investigation);
  const round = roundOf(investigation);
  const maxRounds = maxRoundsOf(investigation);
  // An escalated round, including an evidence-gate escalation, always offers Add evidence.
  const showAddEvidence = !record && (adding || escalated);

  return (
    <section id="decision" aria-label="Decision" className={`${panel} mb-3 scroll-mt-4`}>
      <h2 className="mb-2 text-mps-title font-semibold">
        Decision · Round {round}
        {round > 1 || investigation.maxRounds ? <span className="ml-1 text-mps-label font-normal text-mps-muted">of at most {maxRounds}</span> : null}
      </h2>
      {problem ? (
        <p className="mb-3 text-mps-alarm-text" role="alert">
          {problem}
        </p>
      ) : null}
      {top ? (
        <>
          <p>
            <span className="text-mps-ai">{CATEGORY_LABEL[top.category]}</span> · <Explain text={plainFinding(top.cause)} spellOut={false} />
          </p>
          <EvidenceAgainst counterEvidence={top.counterEvidence} items={investigation.evidence} ruledOut={top.ruledOut} />
          <div className="mt-3" data-testid="recommended-action">
            <h3 className="font-semibold">Recommended action</h3>
            {top.actions.length === 0 ? (
              <p className="text-mps-muted">The agents named a cause but no action; request more evidence.</p>
            ) : (
              <ul className="mt-1 grid gap-1">
                {top.actions.map((action) => {
                  const kind = /^containment:/i.test(action) ? "Containment" : /^preventive:/i.test(action) ? "Preventive" : null;
                  const text = kind ? action.slice(action.indexOf(":") + 1).trim() : action;
                  return (
                    <li key={action}>
                      {kind ? <span className="mr-1 font-semibold">{kind}:</span> : null}
                      <Explain text={plainFinding(text)} spellOut={false} />
                    </li>
                  );
                })}
              </ul>
            )}
          </div>
        </>
      ) : (
        <p>
          <Explain text="No recommendation is ready. An escalation does not create a CAPA." spellOut={false} />
        </p>
      )}
      {record ? <DecisionRecordView record={record} round={round} /> : null}
      {!record && escalated ? (
        <div className="mt-4 rounded border border-mps-line p-3" data-testid="escalation-gate">
          <h3 className="font-semibold">Waiting for more evidence</h3>
          <p className="mt-1 text-mps-secondary">
            <Explain
              text={
                canAddRound(investigation)
                  ? "The agents could not support a cause with the evidence they found, so this round cannot go straight to a CAPA. Add what you know below and the agents run again, or override with a written reason. Both are kept in the audit trail."
                  : "The agents could not support a cause with the evidence they found. This ticket has used every round, so override with a written reason or reject. Both are kept in the audit trail."
              }
            />
          </p>
        </div>
      ) : null}
      {record ? null : (
        <>
      <label className="mt-3 block">
        <span className="text-mps-secondary">Note or justification</span>
        <textarea
          aria-label="Note or justification"
          className="mt-1 block w-full rounded border border-mps-line bg-transparent p-2"
          rows={3}
          value={note}
          onChange={(event) => setNote(event.target.value)}
        />
      </label>
      <label className="mt-2 block">
        <span className="text-mps-secondary">Approver name</span>
        <input aria-label="Approver name" className="mt-1 block w-full rounded border border-mps-line bg-transparent p-2" value={approverId} readOnly title="The approver is the signed-in user." />
      </label>
      <div className="mt-3 flex flex-wrap gap-2">
        <button type="button" className={btnOk} disabled={pending || !top || escalated} onClick={() => setMode("approve")}>
          <span>
            Approve <Term id="CAPA" spellOut={false} />
          </span>
        </button>
        {escalated ? null : (
          <button
            type="button"
            className={btnInfo}
            disabled={pending || adding || !canAddRound(investigation)}
            title={canAddRound(investigation) ? undefined : `This ticket has reached ${maxRounds} rounds, the most allowed.`}
            onClick={() => setAdding(true)}
          >
            Request more evidence
          </button>
        )}
        {escalated ? (
          <button type="button" className={btnDanger} disabled={pending || !justified || !approverId.trim()} onClick={() => void decide("override", note.trim())}>
            Override with justification
          </button>
        ) : null}
        <button type="button" className={btnDanger} disabled={pending} onClick={() => setMode("reject")}>
          Reject
        </button>
      </div>
      {escalated && !justified ? <p className="mt-2 font-mono text-mps-micro text-mps-muted">Override needs at least {limitText(limits()?.overrideMinChars)} characters of justification.</p> : null}
      {showAddEvidence ? (
        <AddEvidenceBox
          investigation={investigation}
          pending={pending}
          onSubmit={(text) => void decide("requestInfo", text)}
          onCancel={escalated ? undefined : () => setAdding(false)}
        />
      ) : null}
        </>
      )}
      <p className="mt-3 text-mps-secondary">Stage: {INVESTIGATION_STAGE_LABEL[investigation.stage]}</p>
      {mode ? (
        <ApprovalDialog
          mode={mode}
          approverId={approverId}
          onCancel={() => setMode(null)}
          onApprove={() => void decide("approve", note.trim() || undefined)}
          onReject={(comment) => void decide("reject", comment)}
        />
      ) : null}
    </section>
  );
}

const DECISION_LABEL: Record<HumanDecisionKind, string> = {
  approve: "Approved: CAPA",
  override: "Overridden with justification",
  reject: "Rejected",
  requestInfo: "More evidence added",
};

/** The decision on file. Older records without one still count as decided once approved or rejected. */
function recordedDecision(investigation: Investigation): DecisionRecord | null {
  if (investigation.decision) return investigation.decision;
  if (investigation.stage === "approved") return { kind: "approve", decidedAt: "" };
  if (investigation.stage === "rejected") return { kind: "reject", decidedAt: "" };
  return null;
}

function decidedAtLabel(value: string): string {
  if (!value) return "Not recorded";
  const at = new Date(value);
  return Number.isNaN(at.getTime()) ? value : at.toLocaleString();
}

/** Read-only record of a final decision. The action buttons are gone once a decision is made. */
function DecisionRecordView({ record, round }: { record: DecisionRecord; round: number }) {
  return (
    <div className="mt-4 rounded border border-mps-line p-3" data-testid="decision-record">
      <h3 className="font-semibold">Decision on file</h3>
      <dl className="mt-2 grid grid-cols-[max-content_1fr] gap-x-4 gap-y-1">
        <dt className="text-mps-secondary">Decision</dt>
        <dd>{DECISION_LABEL[record.kind] ?? record.kind}</dd>
        <dt className="text-mps-secondary">{record.kind === "requestInfo" ? "Evidence added" : "Note or justification"}</dt>
        <dd className="whitespace-pre-line">{record.note?.trim() ? record.note : "No note was written."}</dd>
        <dt className="text-mps-secondary">Approver</dt>
        <dd>{record.decidedBy?.trim() ? record.decidedBy : "Not recorded"}</dd>
        <dt className="text-mps-secondary">Time</dt>
        <dd>{decidedAtLabel(record.decidedAt)}</dd>
      </dl>
      <p className="mt-2 text-mps-secondary">
        {record.kind === "requestInfo"
          ? `Round ${round} is closed and kept as history. The agents ran again with this evidence as round ${round + 1}.`
          : record.kind === "reject"
            ? `The decision for round ${round} is final. The ticket closed with no action.`
            : `The decision for round ${round} is final and cannot be changed.`}
      </p>
    </div>
  );
}
