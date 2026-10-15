// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company
import { useEffect, useState } from "react";
import { Link, useNavigate, useParams } from "react-router-dom";
import { api, ApiProblem } from "../api/client";
import { CATEGORY_LABEL, INVESTIGATION_STAGE_LABEL } from "../api/labels";
import type { HumanDecisionKind, Investigation } from "../api/types";
import { AgentTimeline } from "../components/AgentTimeline";
import { ApprovalDialog, type DecisionMode } from "../components/ApprovalDialog";
import { EvidencePack } from "../components/EvidencePack";
import { Explain } from "../components/Explain";
import { HistoryNote } from "../components/RecordList";
import { Term } from "../components/Term";
import { btnDanger, btnOk, panel } from "../components/ui";
import { plainNarrative } from "../investigation/format";
import { useConsole, useSetLifecycle } from "../context/ConsoleContext";
import { hiddenMissingTitle, MISSING_INVESTIGATION, visibleProblem } from "../history/copy";
import { InvestigationHistory } from "../history/InvestigationHistory";
import { lifecycleForInvestigation } from "../lifecycle";

/** Matches HumanDecisionService.MinOverrideJustification (ADR-005). */
export const OVERRIDE_MIN = 20;

export function ApprovalPage() {
  const { investigationId = "" } = useParams();
  const navigate = useNavigate();
  const { rememberCapa } = useConsole();
  const [investigation, setInvestigation] = useState<Investigation | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [missing, setMissing] = useState(false);
  const [mode, setMode] = useState<DecisionMode | null>(null);
  const [approverId, setApproverId] = useState("dev-user");
  const [pending, setPending] = useState(false);
  const [note, setNote] = useState("");
  useSetLifecycle(investigation?.stage === "approved" ? "capa" : "approval", investigation ? lifecycleForInvestigation(investigation.stage).escalated : false);

  useEffect(() => {
    if (!investigationId) return undefined;
    let cancelled = false;
    setInvestigation(null);
    setError(null);
    setMissing(false);
    if (!investigationId) return () => {
      cancelled = true;
    };
    api
      .getInvestigation(investigationId)
      .then((next) => {
        if (!cancelled) setInvestigation(next);
      })
      .catch((reason: unknown) => {
        if (cancelled) return;
        if (reason instanceof ApiProblem && (reason.status === 404 || hiddenMissingTitle(reason.title))) {
          setMissing(true);
          return;
        }
        setError(reason instanceof ApiProblem ? reason.title : "Investigation did not load.");
      });
    return () => {
      cancelled = true;
    };
  }, [investigationId]);

  async function decide(kind: HumanDecisionKind, comment?: string) {
    setPending(true);
    setError(null);
    try {
      const result = await api.submitDecision(investigationId, {
        kind,
        approverId: kind === "approve" || kind === "override" ? approverId.trim() : undefined,
        comment: comment ?? null,
      });
      setInvestigation(result.investigation);
      setMode(null);
      if (kind === "requestInfo" && result.investigation.id !== investigationId) {
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
  const escalated = investigation?.stage === "escalated";
  const justified = note.trim().length >= OVERRIDE_MIN;
  const top = investigation?.hypotheses.slice().sort((a, b) => b.confidence - a.confidence)[0];

  if (!investigation) {
    if (investigationId && !missing && !error) return <p className="text-mps-muted">Loading approval…</p>;
    return (
      <div>
        <div className="mb-4">
          <h1 className="text-mps-display font-semibold">Approval</h1>
          <p className="max-w-3xl text-mps-secondary">
            <Explain text="Past investigations, newest first. Open one to approve, reject, or ask for more evidence. The model never holds write authority." />
          </p>
        </div>
        {missing ? <HistoryNote text={MISSING_INVESTIGATION} /> : null}
        {problem ? (
          <p className="mb-3 text-mps-alarm-text" role="alert">
            {problem}
          </p>
        ) : null}
        <InvestigationHistory detail="approval" />
      </div>
    );
  }

  return (
    <div>
      <div className="mb-4">
        <p className="font-mono text-mps-micro uppercase tracking-wider text-mps-muted">
          <Link to={`/investigations/${investigationId}`}>Investigation</Link> · human-gated write
        </p>
        <h1 className="text-mps-display font-semibold">Approval</h1>
        <p className="max-w-3xl text-mps-secondary">
          <Explain text="The model never holds write authority. Approve records the approver name and creates the CAPA." />
        </p>
      </div>
      {problem ? (
        <p className="mb-3 text-mps-alarm-text" role="alert">
          {problem}
        </p>
      ) : null}
      <div className="grid gap-3 lg:grid-cols-2">
        <section className={panel}>
          <h2 className="mb-2 text-mps-title font-semibold">Recommended action</h2>
          {top ? (
            <>
              <p>
                <span className="text-mps-ai">{CATEGORY_LABEL[top.category]}</span> · {plainNarrative(top.cause)}
              </p>
              <ul className="mt-2 list-disc pl-5">
                {top.actions.map((action) => (
                  <li key={action}>{plainNarrative(action)}</li>
                ))}
              </ul>
              {top.counterEvidence.length > 0 ? (
                <p className="mt-2">Evidence against this: {top.counterEvidence.map((line) => plainNarrative(line)).join(" ")}</p>
              ) : null}
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
              {investigation?.escalationReason ? (
                <p className="mt-2 italic">
                  <Explain text={plainNarrative(investigation.escalationReason)} spellOut={false} />
                </p>
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
                <input aria-label="Approver name" className="mt-1 block w-full rounded border border-mps-line bg-transparent p-2" value={approverId} onChange={(event) => setApproverId(event.target.value)} />
              </label>
              <div className="mt-3 flex flex-wrap gap-2">
                <button type="button" className={btnOk} disabled={pending} onClick={() => void decide("requestInfo", note.trim() || "More evidence requested.")}>
                  Request more evidence
                </button>
                <button type="button" className={btnDanger} disabled={pending || !justified || !approverId.trim()} onClick={() => void decide("override", note.trim())}>
                  Override with justification
                </button>
              </div>
              {!justified ? <p className="mt-2 font-mono text-mps-micro text-mps-muted">Override needs at least {OVERRIDE_MIN} characters of justification.</p> : null}
            </div>
          ) : null}
          <div className="mt-4 flex flex-wrap gap-2">
            <button type="button" className={btnOk} disabled={pending || !top || escalated} onClick={() => setMode("approve")}>
              Approve <Term id="CAPA" spellOut={false} /> + containment
            </button>
            <button type="button" className={btnDanger} disabled={pending || !investigation} onClick={() => setMode("reject")}>
              Reject
            </button>
            {!escalated ? (
              <button type="button" className="text-mps-nominal underline" disabled={pending || !investigation} onClick={() => void decide("requestInfo", "Need another evidence source.")}>
                Request info
              </button>
            ) : null}
          </div>
          {investigation ? <p className="mt-3 text-mps-secondary">Stage: {INVESTIGATION_STAGE_LABEL[investigation.stage]}</p> : null}
        </section>
        <section className={panel}>
          <h2 className="mb-2 text-mps-title font-semibold">
            Enforcement <span className="float-right font-mono text-mps-micro text-mps-nominal">deterministic</span>
          </h2>
          <table className="w-full text-left">
            <tbody>
              <tr className="border-t border-mps-line"><td className="py-1.5">No approver name</td><td className="py-1.5 font-mono">400</td></tr>
              <tr className="border-t border-mps-line"><td className="py-1.5">Used or expired approval</td><td className="py-1.5 font-mono">rejected</td></tr>
              <tr className="border-t border-mps-line"><td className="py-1.5">Recorded action does not match</td><td className="py-1.5 font-mono">rejected</td></tr>
              <tr className="border-t border-mps-line"><td className="py-1.5">Agent write tool</td><td className="py-1.5">not registered</td></tr>
              <tr className="border-t border-mps-line"><td className="py-1.5">The browser does not sign the approval</td><td className="py-1.5">ignored</td></tr>
            </tbody>
          </table>
        </section>
      </div>
      {investigation ? (
        <>
          <section className={`${panel} mt-3`}>
            <EvidencePack items={investigation.evidence} />
          </section>
          <section className={`${panel} mt-3`}>
            <AgentTimeline investigation={investigation} />
          </section>
        </>
      ) : null}
      {mode ? (
        <ApprovalDialog
          mode={mode}
          approverId={approverId}
          onApproverId={setApproverId}
          onCancel={() => setMode(null)}
          onApprove={() => void decide("approve")}
          onReject={(comment) => void decide("reject", comment)}
        />
      ) : null}
    </div>
  );
}
