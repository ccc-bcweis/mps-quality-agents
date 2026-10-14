// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company
import { useEffect, useState } from "react";
import { Link, useParams } from "react-router-dom";
import { api, ApiProblem } from "../api/client";
import { CATEGORY_LABEL } from "../api/labels";
import type { Investigation } from "../api/types";
import { btnPrimary, panel } from "../components/ui";
import { useSetLifecycle } from "../context/ConsoleContext";
import { lifecycleForInvestigation } from "../lifecycle";
import { buildTrace } from "../trace";

export function InvestigationPage() {
  const { investigationId = "" } = useParams();
  const [investigation, setInvestigation] = useState<Investigation | null>(null);
  const [error, setError] = useState<string | null>(null);
  const view = investigation ? lifecycleForInvestigation(investigation.stage) : { current: "investigation" as const, escalated: false };
  useSetLifecycle(view.current, view.escalated);

  useEffect(() => {
    let cancelled = false;
    setInvestigation(null);
    setError(null);
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

  if (error) return <p className="text-mps-alarm-text">{error}</p>;
  if (!investigation) return <p className="text-mps-muted">Loading investigation…</p>;

  const trace = buildTrace(investigation);
  const ranked = [...investigation.hypotheses].sort((a, b) => b.confidence - a.confidence);

  return (
    <div>
      <div className="mb-4 flex flex-wrap items-end justify-between gap-3">
        <div>
          <p className="font-mono text-mps-micro uppercase tracking-wider text-mps-muted">{investigation.correlationId}</p>
          <h1 className="text-mps-display font-semibold">Investigation</h1>
          <p className="max-w-3xl text-mps-secondary">Read-only evidence, then ranked hypotheses. Violet marks the resolution agent. Cyan marks deterministic rules. The model cannot write.</p>
        </div>
        {investigation.stage === "recommendationReady" ? (
          <Link className={btnPrimary} to={`/approvals/${investigation.id}`}>
            Review recommendation
          </Link>
        ) : null}
      </div>
      {investigation.escalationReason ? (
        <p className="mb-3 rounded border border-mps-warn bg-mps-panel px-3 py-2" role="status">
          Evidence gate: <strong>escalate</strong>. {investigation.escalationReason}
        </p>
      ) : (
        <p className="mb-3 rounded border border-mps-ok bg-mps-panel px-3 py-2">
          Evidence gate: <strong>sufficient</strong> for a recommendation. Confounders are listed on the hypothesis and have to be addressed.
        </p>
      )}
      <div className="grid gap-3 lg:grid-cols-3">
        <section className={panel}>
          <h2 className="mb-2 text-mps-title font-semibold">
            Evidence pack <span className="float-right font-mono text-mps-micro text-mps-muted">{investigation.evidence.length}</span>
          </h2>
          <ul className="space-y-2">
            {investigation.evidence.map((item) => (
              <li key={item.id} className="border-t border-mps-line pt-2">
                <div className="font-mono text-mps-micro text-mps-nominal">{item.externalRef ?? item.id.slice(0, 8)}</div>
                <div className="font-medium">{item.title}</div>
                <p className="text-mps-secondary">{item.excerpt}</p>
                <p className="font-mono text-mps-micro text-mps-muted">{item.source}</p>
              </li>
            ))}
          </ul>
        </section>
        <section className={panel}>
          <h2 className="mb-2 text-mps-title font-semibold">
            Hypotheses <span className="float-right font-mono text-mps-micro text-mps-ai">Resolution agent</span>
          </h2>
          {ranked.length === 0 ? <p>No root cause is asserted.</p> : null}
          <ul className="space-y-3">
            {ranked.map((hypothesis, index) => (
              <li key={hypothesis.id} className={`border-t border-mps-line pt-2 ${index === 0 ? "border-l-2 border-l-mps-ai pl-2" : "opacity-80"}`}>
                <div className="font-mono text-mps-micro text-mps-ai">
                  H{index + 1} · {hypothesis.confidence.toFixed(2)} · {CATEGORY_LABEL[hypothesis.category]}
                </div>
                <p>{hypothesis.cause}</p>
                {hypothesis.counterEvidence.length > 0 ? (
                  <p className="mt-1">
                    <strong>counterEvidence:</strong> {hypothesis.counterEvidence.join(" ")}
                  </p>
                ) : null}
              </li>
            ))}
          </ul>
        </section>
        <section className={panel}>
          <h2 className="mb-2 text-mps-title font-semibold">Trace</h2>
          <ol className="space-y-2">
            {trace.map((row) => (
              <li key={row.id} className="border-t border-mps-line pt-2">
                <div className={`font-mono text-mps-micro uppercase ${row.kind === "ai" ? "text-mps-ai" : row.kind === "human" ? "text-mps-warn" : "text-mps-nominal"}`}>
                  {row.kind === "ai" ? "AI" : row.kind === "human" ? "Human" : "Rules"}
                </div>
                <div className="font-medium">{row.title}</div>
                <p className="text-mps-secondary">{row.detail}</p>
              </li>
            ))}
          </ol>
        </section>
      </div>
    </div>
  );
}
