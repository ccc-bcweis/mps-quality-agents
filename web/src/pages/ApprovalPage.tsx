// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company
import { useEffect, useState } from "react";
import { Link, useNavigate, useParams } from "react-router-dom";
import { api, ApiProblem } from "../api/client";
import { CATEGORY_LABEL } from "../api/labels";
import type { Investigation } from "../api/types";
import { ApprovalDialog, type DecisionMode } from "../components/ApprovalDialog";
import { btnDanger, btnOk, panel } from "../components/ui";
import { useConsole, useSetLifecycle } from "../context/ConsoleContext";
import { lifecycleForInvestigation } from "../lifecycle";

export function ApprovalPage() {
  const { investigationId = "" } = useParams();
  const navigate = useNavigate();
  const { rememberCapa } = useConsole();
  const [investigation, setInvestigation] = useState<Investigation | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [mode, setMode] = useState<DecisionMode | null>(null);
  const [approverId, setApproverId] = useState("dev-user");
  const [pending, setPending] = useState(false);
  useSetLifecycle(investigation?.stage === "approved" ? "capa" : "approval", investigation ? lifecycleForInvestigation(investigation.stage).escalated : false);

  useEffect(() => {
    let cancelled = false;
    api
      .getInvestigation(investigationId)
      .then((next) => {
        if (!cancelled) setInvestigation(next);
      })
      .catch((reason: unknown) => {
        if (!cancelled) setError(reason instanceof ApiProblem ? reason.title : "Investigation did not load.");
      });
    return () => {
      cancelled = true;
    };
  }, [investigationId]);

  async function decide(kind: "approve" | "reject" | "requestInfo", comment?: string) {
    setPending(true);
    setError(null);
    try {
      const result = await api.submitDecision(investigationId, {
        kind,
        approverId: kind === "approve" ? approverId.trim() : undefined,
        comment: comment ?? null,
      });
      setInvestigation(result.investigation);
      setMode(null);
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

  const top = investigation?.hypotheses.slice().sort((a, b) => b.confidence - a.confidence)[0];

  return (
    <div>
      <div className="mb-4">
        <p className="font-mono text-mps-micro uppercase tracking-wider text-mps-muted">
          <Link to={`/investigations/${investigationId}`}>Investigation</Link> · human-gated write
        </p>
        <h1 className="text-mps-display font-semibold">Approval</h1>
        <p className="max-w-3xl text-mps-secondary">The model never holds write authority. Approve sends an approver id. The server issues the token and creates the CAPA.</p>
      </div>
      {error ? (
        <p className="mb-3 text-mps-alarm-text" role="alert">
          {error}
        </p>
      ) : null}
      <div className="grid gap-3 lg:grid-cols-2">
        <section className={panel}>
          <h2 className="mb-2 text-mps-title font-semibold">Recommended action</h2>
          {top ? (
            <>
              <p>
                <span className="font-mono text-mps-micro text-mps-ai">{CATEGORY_LABEL[top.category]}</span> · {top.cause}
              </p>
              <ul className="mt-2 list-disc pl-5">
                {top.actions.map((action) => (
                  <li key={action}>{action}</li>
                ))}
              </ul>
              {top.counterEvidence.length > 0 ? <p className="mt-2">counterEvidence: {top.counterEvidence.join(" ")}</p> : null}
            </>
          ) : (
            <p>No recommendation is ready. An escalation does not create a CAPA.</p>
          )}
          <div className="mt-4 flex flex-wrap gap-2">
            <button type="button" className={btnOk} disabled={pending || !top} onClick={() => setMode("approve")}>
              Approve CAPA + containment
            </button>
            <button type="button" className={btnDanger} disabled={pending || !investigation} onClick={() => setMode("reject")}>
              Reject
            </button>
            <button type="button" className="text-mps-nominal underline" disabled={pending || !investigation} onClick={() => void decide("requestInfo", "Need another evidence source.")}>
              Request info
            </button>
          </div>
          {investigation ? <p className="mt-3 font-mono text-mps-micro text-mps-muted">Stage {investigation.stage}</p> : null}
        </section>
        <section className={panel}>
          <h2 className="mb-2 text-mps-title font-semibold">
            Enforcement <span className="float-right font-mono text-mps-micro text-mps-nominal">deterministic</span>
          </h2>
          <table className="w-full text-left">
            <tbody>
              <tr className="border-t border-mps-line"><td className="py-1.5">No approver id</td><td className="py-1.5 font-mono">400</td></tr>
              <tr className="border-t border-mps-line"><td className="py-1.5">Tampered, expired, or reused token</td><td className="py-1.5 font-mono">rejected</td></tr>
              <tr className="border-t border-mps-line"><td className="py-1.5">Hash mismatch</td><td className="py-1.5 font-mono">rejected</td></tr>
              <tr className="border-t border-mps-line"><td className="py-1.5">Agent write tool</td><td className="py-1.5">not registered</td></tr>
              <tr className="border-t border-mps-line"><td className="py-1.5">Client-supplied signature</td><td className="py-1.5">ignored</td></tr>
            </tbody>
          </table>
        </section>
      </div>
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
