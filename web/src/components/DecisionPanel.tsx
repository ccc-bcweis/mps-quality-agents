// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company
import { useEffect, useState } from "react";
import { useNavigate } from "react-router-dom";
import { api, ApiProblem } from "../api/client";
import { CATEGORY_LABEL, INVESTIGATION_STAGE_LABEL } from "../api/labels";
import type { HumanDecisionKind, Investigation } from "../api/types";
import { ApprovalDialog, type DecisionMode } from "./ApprovalDialog";
import { Explain } from "./Explain";
import { Term } from "./Term";
import { btnDanger, btnOk, btnPrimary, panel } from "./ui";
import { plainFinding } from "../investigation/format";
import { useConsole } from "../context/ConsoleContext";
import { visibleProblem } from "../history/copy";

/** Matches HumanDecisionService.MinOverrideJustification (ADR-005). */
export const OVERRIDE_MIN = 20;

/** The human decision at the gate result: approve, reject, request more evidence, or override an escalation. */
export function DecisionPanel({ investigation, onChange }: { investigation: Investigation; onChange: (next: Investigation) => void }) {
  const navigate = useNavigate();
  const { rememberCapa } = useConsole();
  const [mode, setMode] = useState<DecisionMode | null>(null);
  const [approverId, setApproverId] = useState("");
  const [pending, setPending] = useState(false);
  const [note, setNote] = useState("");
  const [error, setError] = useState<string | null>(null);
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
        approverId: kind === "approve" || kind === "override" ? approverId.trim() : undefined,
        comment: comment ?? null,
      });
      onChange(result.investigation);
      setMode(null);
      if (kind === "requestInfo" && result.investigation.id !== investigation.id) {
        navigate(`/investigations/${result.investigation.id}`);
        return;
      }
      if (result.capa) {
        rememberCapa(result.capa);
        navigate(`/capas/${result.capa.id}`);
      }
    } catch (reason: unknown) {
      setError(reason instanceof ApiProblem ? reason.title : "The decision was not accepted.");
    } finally {
      setPending(false);
    }
  }

  const problem = visibleProblem(error);
  const escalated = investigation.stage === "escalated";
  const justified = note.trim().length >= OVERRIDE_MIN;
  const top = investigation.hypotheses.slice().sort((a, b) => b.confidence - a.confidence)[0];

  return (
    <section id="decision" aria-label="Decision" className={`${panel} mb-3 scroll-mt-4`}>
      <h2 className="mb-2 text-mps-title font-semibold">Decision</h2>
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
      {escalated ? (
        <div className="mt-4 rounded border border-mps-line p-3" data-testid="escalation-gate">
          <h3 className="font-semibold">The agents escalated this case</h3>
          <p className="mt-1 text-mps-secondary">
            <Explain text="An escalated case cannot go straight to a CAPA. Ask the agents to look again with more evidence, or override with a written reason. The reason is kept in the audit trail." />
          </p>
        </div>
      ) : null}
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
          Approve <Term id="CAPA" spellOut={false} /> + containment
        </button>
        <button type="button" className={btnPrimary} disabled={pending} onClick={() => void decide("requestInfo", note.trim() || "More evidence requested.")}>
          Request more evidence
        </button>
        {escalated ? (
          <button type="button" className={btnDanger} disabled={pending || !justified || !approverId.trim()} onClick={() => void decide("override", note.trim())}>
            Override with justification
          </button>
        ) : null}
        <button type="button" className={btnDanger} disabled={pending} onClick={() => setMode("reject")}>
          Reject
        </button>
      </div>
      {escalated && !justified ? <p className="mt-2 font-mono text-mps-micro text-mps-muted">Override needs at least {OVERRIDE_MIN} characters of justification.</p> : null}
      <p className="mt-3 text-mps-secondary">Stage: {INVESTIGATION_STAGE_LABEL[investigation.stage]}</p>
      {mode ? (
        <ApprovalDialog
          mode={mode}
          approverId={approverId}
          onCancel={() => setMode(null)}
          onApprove={() => void decide("approve")}
          onReject={(comment) => void decide("reject", comment)}
        />
      ) : null}
    </section>
  );
}
