// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company
import { useEffect, useState } from "react";
import { Link, useParams } from "react-router-dom";
import { api, ApiProblem } from "../api/client";
import { CATEGORY_LABEL, CHANNEL_LABEL, STAGE_LABEL } from "../api/labels";
import type { Incident, Investigation } from "../api/types";
import { AgentTimeline } from "../components/AgentTimeline";
import { EvidenceMatrix } from "../components/EvidenceMatrix";
import { Explain } from "../components/Explain";
import { Term } from "../components/Term";
import { btnPrimary, panel } from "../components/ui";
import { useSetLifecycle } from "../context/ConsoleContext";
import { useSite } from "../context/SiteContext";
import { toolInPlant } from "../sites/selection";
import { lifecycleForIncident, lifecycleForInvestigation } from "../lifecycle";
import { INVESTIGATION_POLL_MS } from "../investigation/timeline";
import { buildTrace } from "../trace";

type LoadResult = "ready" | "missing" | "failed";

function plantToolId(investigation: Investigation | null, incident: Incident | null): string | null {
  if (incident?.toolId) return incident.toolId;
  return investigation?.evidence.find((item) => item.toolId)?.toolId ?? null;
}

export function InvestigationPage() {
  const params = useParams();
  const { siteId } = useSite();
  const routeInvestigationId = params.investigationId ?? "";
  const routeIncidentId = params.incidentId ?? "";
  const [investigation, setInvestigation] = useState<Investigation | null>(null);
  const [incident, setIncident] = useState<Incident | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [ready, setReady] = useState(false);
  const [starting, setStarting] = useState(false);
  const [startError, setStartError] = useState<string | null>(null);
  const view = investigation
    ? lifecycleForInvestigation(investigation.stage)
    : {
        current: "investigation" as const,
        escalated: incident ? lifecycleForIncident(incident.stage).escalated : false,
      };
  useSetLifecycle(view.current, view.escalated);

  useEffect(() => {
    let cancelled = false;
    setInvestigation(null);
    setIncident(null);
    setError(null);
    setReady(false);
    setStartError(null);

    if (!routeIncidentId && !routeInvestigationId) {
      setReady(true);
      return;
    }

    const applyError = (reason: unknown) => {
      if (cancelled) return;
      setError(reason instanceof ApiProblem ? reason.title : "Investigation did not load.");
    };

    const loadIncident = async (id: string): Promise<LoadResult> => {
      try {
        const loaded = await api.getIncident(id);
        if (cancelled) return "ready";
        setIncident(loaded);
      } catch (reason: unknown) {
        if (reason instanceof ApiProblem && reason.status === 404) return "missing";
        applyError(reason);
        return "failed";
      }

      try {
        const latest = await api.getLatestInvestigation(id);
        if (!cancelled) setInvestigation(latest);
      } catch (reason: unknown) {
        if (reason instanceof ApiProblem && reason.status === 404) return "ready";
        applyError(reason);
        return "failed";
      }

      return "ready";
    };

    const run = async () => {
      if (routeIncidentId) {
        const result = await loadIncident(routeIncidentId);
        if (result === "missing" && !cancelled) setError("No such record.");
        return;
      }

      try {
        const loaded = await api.getInvestigation(routeInvestigationId);
        if (!cancelled) setInvestigation(loaded);
        try {
          const loadedIncident = await api.getIncident(loaded.incidentId);
          if (!cancelled) setIncident(loadedIncident);
        } catch {
          // Evidence still names the tool when the incident lookup misses.
        }
      } catch (reason: unknown) {
        if (reason instanceof ApiProblem && reason.status === 404) {
          const result = await loadIncident(routeInvestigationId);
          if (result === "missing" && !cancelled) setError(reason.title);
          return;
        }
        applyError(reason);
      }
    };

    void run().finally(() => {
      if (!cancelled) setReady(true);
    });

    return () => {
      cancelled = true;
    };
  }, [routeIncidentId, routeInvestigationId]);

  const liveId = investigation?.id ?? "";
  useEffect(() => {
    if (!liveId) return undefined;
    let cancelled = false;
    const timer = window.setInterval(() => {
      api
        .getInvestigation(liveId)
        .then((next) => {
          if (!cancelled) setInvestigation(next);
        })
        .catch(() => {
          // A missed poll keeps the last snapshot on screen.
        });
    }, INVESTIGATION_POLL_MS);
    return () => {
      cancelled = true;
      window.clearInterval(timer);
    };
  }, [liveId]);

  async function onStart() {
    if (!incident || starting) return;
    setStarting(true);
    setStartError(null);
    try {
      const started = await api.startInvestigation(incident.id);
      setInvestigation(started);
    } catch (reason: unknown) {
      setStartError(reason instanceof ApiProblem ? reason.title : "The investigation did not start.");
    } finally {
      setStarting(false);
    }
  }

  if (!routeIncidentId && !routeInvestigationId) {
    return (
      <div>
        <h1 className="text-mps-display font-semibold">Investigation</h1>
        <p className="max-w-3xl text-mps-secondary">No investigation is open. Faults show up here after a scenario or a manual injection.</p>
      </div>
    );
  }

  if (!ready) return <p className="text-mps-muted">Loading investigation…</p>;
  const homeTool = plantToolId(investigation, incident);
  if (homeTool && !toolInPlant(siteId, homeTool)) {
    return (
      <div>
        <h1 className="text-mps-display font-semibold">Investigation</h1>
        <p>This investigation is at another plant. Choose All sites to open it.</p>
      </div>
    );
  }
  if (investigation) return <InvestigationDetail investigation={investigation} />;
  if (incident && !error) {
    const finished = incident.stage === "closed" || incident.stage === "approved";
    return (
      <div>
        <div className="mb-4">
          <h1 className="text-mps-display font-semibold">Investigation</h1>
          <p className="max-w-3xl text-mps-secondary">No investigation has started for this ticket. Starting one gathers the evidence and shows the agent steps.</p>
        </div>
        <section className={panel}>
          <p>
            <Explain text={CHANNEL_LABEL[incident.channel]} spellOut={false} /> · tool {incident.toolId}
          </p>
          <p className="mt-2">
            <Explain text={incident.summary} />
          </p>
          <p className="mt-2 text-mps-secondary">Stage: {STAGE_LABEL[incident.stage]}</p>
        </section>
        {finished ? (
          <p className="mt-3">This ticket is finished. There is no investigation to open.</p>
        ) : (
          <button className={`${btnPrimary} mt-3`} type="button" onClick={() => void onStart()} disabled={starting}>
            {starting ? "Starting…" : "Start investigation"}
          </button>
        )}
        {startError ? <p className="mt-3 text-mps-alarm-text">{startError}</p> : null}
      </div>
    );
  }

  return <p className="text-mps-alarm-text">{error ?? "Investigation did not load."}</p>;
}

function InvestigationDetail({ investigation }: { investigation: Investigation }) {
  const trace = buildTrace(investigation);
  const ranked = [...investigation.hypotheses].sort((a, b) => b.confidence - a.confidence);

  return (
    <div>
      <div className="mb-4 flex flex-wrap items-end justify-between gap-3">
        <div>
          <h1 className="text-mps-display font-semibold">Investigation</h1>
          <p className="max-w-3xl text-mps-secondary">
            <Explain text="Read-only evidence, then ranked hypotheses. AI proposes a ranking. Violet marks the model. Cyan marks deterministic rules. The model cannot write." />
          </p>
        </div>
        {investigation.stage === "recommendationReady" ? (
          <Link className={btnPrimary} to={`/approvals/${investigation.id}`}>
            Review recommendation
          </Link>
        ) : null}
      </div>
      {investigation.escalationReason ? (
        <p className="mb-3 rounded border border-mps-warn bg-mps-panel px-3 py-2" role="status">
          Evidence gate: <strong>escalate</strong>. <Explain text={investigation.escalationReason ?? ""} />
        </p>
      ) : (
        <p className="mb-3 rounded border border-mps-ok bg-mps-panel px-3 py-2">
          Evidence gate: <strong>sufficient</strong> for a recommendation. Confounders are listed on the hypothesis and have to be addressed.
        </p>
      )}
      <section className={`${panel} mb-3`}>
        <AgentTimeline investigation={investigation} />
      </section>
      <div className="grid gap-3 lg:grid-cols-3">
        <section className={panel}>
          <h2 className="mb-2 text-mps-title font-semibold">
            Evidence pack <span className="float-right font-mono text-mps-micro text-mps-muted">{investigation.evidence.length}</span>
          </h2>
          <ul className="space-y-2">
            {investigation.evidence.map((item) => (
              <li key={item.id} className="border-t border-mps-line pt-2">
                <div className="font-mono text-mps-micro text-mps-nominal">{item.externalRef ?? item.id.slice(0, 8)}</div>
                <div className="font-medium">
                  <Explain text={item.title} spellOut={false} />
                </div>
                <p className="text-mps-secondary">
                  <Explain text={item.excerpt} />
                </p>
                {item.whyItMatters ? <p className="text-mps-secondary">Why it matters: {item.whyItMatters}</p> : null}
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
                <p>
                  <Explain text={hypothesis.cause} spellOut={false} />
                </p>
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
                  {row.kind === "ai" ? <Term id="AI" spellOut={false} /> : row.kind === "human" ? "Human" : "Rules"}
                </div>
                <div className="font-medium">
                  <Explain text={row.title} spellOut={false} />
                </div>
                <p className="text-mps-secondary">
                  <Explain text={row.detail} spellOut={false} />
                </p>
              </li>
            ))}
          </ol>
        </section>
      </div>
      <section className={`${panel} mt-3`}>
        <EvidenceMatrix investigationId={investigation.id} />
      </section>
    </div>
  );
}
