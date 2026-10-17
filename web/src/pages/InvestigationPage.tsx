// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company
import { useEffect, useRef, useState } from "react";
import { useLocation, useParams } from "react-router-dom";
import { api, ApiProblem } from "../api/client";
import { CATEGORY_LABEL, CHANNEL_LABEL, STAGE_LABEL } from "../labels";
import type { Incident, Investigation } from "../api/types";
import { evidenceGateState } from "../investigation/evidenceGate";
import { AgentTimeline } from "../components/AgentTimeline";
import { DecisionPanel } from "../components/DecisionPanel";
import { EvidenceMatrix } from "../components/EvidenceMatrix";
import { EvidencePack } from "../components/EvidencePack";
import { Explain } from "../components/Explain";
import { HistoryNote } from "../components/RecordList";
import { RoundHistory } from "../components/RoundHistory";
import { btnPrimary, panel } from "../components/ui";
import { useSetLifecycle } from "../context/ConsoleContext";
import { hiddenMissingTitle, MISSING_INVESTIGATION, visibleProblem } from "../history/copy";
import { InvestigationHistory } from "../history/InvestigationHistory";
import { useSite } from "../context/SiteContext";
import { toolInPlant } from "../sites/selection";
import { placeLabel, plainFinding, plainNarrative } from "../investigation/format";
import { lifecycleForIncident, lifecycleForInvestigation } from "../lifecycle";
import { FALLBACK_POLL_MS, onHubState, useHubConnected, useHubEvent } from "../live/fleetHub";
import { parameterLabel } from "../labels";

type LoadResult = "ready" | "missing" | "failed";

function InvestigationList({ note }: { note: string | null }) {
  return (
    <div>
      <div className="mb-4">
        <h1 className="text-mps-display font-semibold">Investigation</h1>
        <p className="max-w-3xl text-mps-secondary">Past investigations, newest first. Open one to read the evidence.</p>
      </div>
      {note ? <HistoryNote text={note} /> : null}
      <InvestigationHistory />
    </div>
  );
}

function plantToolId(investigation: Investigation | null, incident: Incident | null): string | null {
  if (incident?.toolId) return incident.toolId;
  return investigation?.evidence.find((item) => item.toolId)?.toolId ?? null;
}

export function InvestigationPage() {
  const params = useParams();
  const { siteId } = useSite();
  const routeInvestigationId = params.investigationId ?? "";
  const routeIncidentId = params.incidentId ?? "";
  const listing = routeInvestigationId.length === 0 && routeIncidentId.length === 0;
  const [investigation, setInvestigation] = useState<Investigation | null>(null);
  const [incident, setIncident] = useState<Incident | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [note, setNote] = useState<string | null>(null);
  const [ready, setReady] = useState(listing);
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
    setNote(null);
    setReady(listing);
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
      if (listing) return;
      if (routeIncidentId) {
        const result = await loadIncident(routeIncidentId);
        if (result === "missing" && !cancelled) setNote(MISSING_INVESTIGATION);
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
          if (result === "missing" && !cancelled) setNote(MISSING_INVESTIGATION);
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
  }, [listing, routeIncidentId, routeInvestigationId]);

  // The investigation streams in over the fleet hub: investigationChanged carries the whole record. The running
  // placeholder and the saved record have different ids, so the match is on the incident. A reconnect or resync reloads;
  // while the stream is down the page reloads every FALLBACK_POLL_MS.
  const liveId = investigation?.id ?? "";
  const liveIncident = investigation?.incidentId ?? incident?.id ?? "";
  useHubEvent("investigationChanged", (payload) => {
    const next = payload as Investigation | null;
    if (!next || !liveIncident || next.incidentId !== liveIncident) return;
    setInvestigation((current) => {
      if (!current || current.id === next.id) return next;
      // A closed round stays on screen as history; Rounds links to the newer round.
      if (current.stage === "superseded" || (current.round && next.round && next.round !== current.round)) return current;
      return current.stage !== "started" && next.stage === "started" ? current : next;
    });
  });
  const connected = useHubConnected();
  const reloadRef = useRef<() => void>(() => undefined);
  reloadRef.current = () => {
    if (!liveId) return;
    api
      .getInvestigation(liveId)
      .then((next) => setInvestigation(next))
      .catch(() => {
        // A missed reload keeps the last snapshot on screen.
      });
  };
  useHubEvent("resync", () => reloadRef.current());
  useEffect(
    () =>
      onHubState((_up, reconnected) => {
        if (reconnected) reloadRef.current();
      }),
    [],
  );
  useEffect(() => {
    if (!liveId || connected) return undefined;
    const timer = window.setInterval(() => reloadRef.current(), FALLBACK_POLL_MS);
    return () => window.clearInterval(timer);
  }, [liveId, connected]);

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

  if (listing || note || hiddenMissingTitle(error)) {
    return <InvestigationList note={note ?? (hiddenMissingTitle(error) ? MISSING_INVESTIGATION : null)} />;
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
  if (investigation) return <InvestigationDetail investigation={investigation} onChange={setInvestigation} />;
  if (incident && !error) {
    const finished = incident.stage === "closed" || incident.stage === "closedByDemoReset" || incident.stage === "approved";
    return (
      <div>
        <div className="mb-4">
          <h1 className="text-mps-display font-semibold" title={incident.correlationId}>
            Investigation
          </h1>
          <p className="max-w-3xl text-mps-secondary">No investigation has started for this ticket. Starting one gathers the evidence and shows the agent steps.</p>
        </div>
        <section className={panel}>
          <p>
            <Explain text={CHANNEL_LABEL[incident.channel]} spellOut={false} /> · {placeLabel(incident.toolId, incident.chamberId)} ·{" "}
            {parameterLabel(incident.parameter)}
          </p>
          <p className="mt-2">
            <Explain text={plainNarrative(incident.summary)} />
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
        {visibleProblem(startError) ? <p className="mt-3 text-mps-alarm-text">{visibleProblem(startError)}</p> : null}
      </div>
    );
  }

  return <p className="text-mps-alarm-text">{visibleProblem(error) ?? "Investigation did not load."}</p>;
}

function InvestigationDetail({ investigation, onChange }: { investigation: Investigation; onChange: (next: Investigation) => void }) {
  const location = useLocation();
  useEffect(() => {
    if (location.hash === "#decision") document.getElementById("decision")?.scrollIntoView?.({ block: "start" });
  }, [location.hash]);
  const ranked = [...investigation.hypotheses].sort((a, b) => b.confidence - a.confidence);
  const place = investigation.evidence[0]
    ? placeLabel(investigation.evidence[0].toolId, null, investigation.evidence[0].toolName, investigation.evidence[0].chamberName)
    : null;

  return (
    <div>
      <div className="mb-4 flex flex-wrap items-end justify-between gap-3">
        <div>
          <h1 className="text-mps-display font-semibold" title={investigation.correlationId}>
            Investigation
          </h1>
          {place && place !== "—" ? <p className="text-mps-secondary">{place}</p> : null}
          <p className="max-w-3xl text-mps-secondary">
            <Explain text="Evidence first, then the ranked causes and your decision. Violet marks the AI. Cyan marks fixed rule checks." />
          </p>
        </div>
        <a className={btnPrimary} href="#decision" onClick={(event) => { event.preventDefault(); document.getElementById("decision")?.scrollIntoView?.({ block: "start" }); }}>
          Go to decision
        </a>
      </div>
      <RoundHistory investigation={investigation} />
      <EvidenceGate investigation={investigation} />
      <DecisionPanel investigation={investigation} onChange={onChange} />
      <section className={`${panel} mb-3`}>
        <AgentTimeline investigation={investigation} />
      </section>
      <section className={`${panel} mb-3`}>
        <EvidencePack items={investigation.evidence} />
      </section>
      <div className="grid gap-3 lg:grid-cols-2">
        <section className={panel}>
          <h2 className="mb-2 text-mps-title font-semibold">Hypotheses</h2>
          {ranked.length === 0 ? <p>No root cause is asserted.</p> : null}
          <ul className="space-y-3">
            {ranked.map((hypothesis, index) => (
              <li key={hypothesis.id} title={hypothesis.id} className={`border-t border-mps-line pt-2 ${index === 0 ? "border-l-2 border-l-mps-ai pl-2" : "opacity-80"}`}>
                <div className="text-mps-secondary">
                  {index + 1} · {hypothesis.confidence.toFixed(2)} · {CATEGORY_LABEL[hypothesis.category]}
                </div>
                <p>
                  <Explain text={plainFinding(hypothesis.cause)} spellOut={false} />
                </p>
                {hypothesis.counterEvidence.length > 0 ? (
                  <p className="mt-1">
                    <strong>Evidence against this:</strong> {hypothesis.counterEvidence.map((line) => plainNarrative(line)).join(" ")}
                  </p>
                ) : null}
              </li>
            ))}
          </ul>
        </section>
        {(investigation.evidenceMatrix?.length ?? 0) > 0 ? (
          <section className={panel}>
            <EvidenceMatrix investigation={investigation} />
          </section>
        ) : null}
      </div>
    </div>
  );
}

function EvidenceGate({ investigation }: { investigation: Investigation }) {
  const state = evidenceGateState(investigation);
  if (state === "escalate") {
    return (
      <p className="mb-3 rounded border border-mps-warn bg-mps-panel px-3 py-2" role="status" data-gate="escalate">
        Evidence gate: <strong>escalate</strong>. <Explain text={plainNarrative(investigation.escalationReason ?? "")} spellOut={false} />
      </p>
    );
  }

  if (state === "sufficient") {
    return (
      <p className="mb-3 rounded border border-mps-ok bg-mps-panel px-3 py-2" role="status" data-gate="sufficient">
        Evidence gate: <strong>sufficient</strong> for a recommendation. Records that look related but are not the cause are listed as ruled out.
      </p>
    );
  }

  if (state === "pending") {
    return (
      <p className="mb-3 rounded border border-mps-line bg-mps-panel px-3 py-2" role="status" data-gate="pending">
        Evidence gate: <strong>pending</strong>. Triage is still gathering evidence; the gate is checked once the evidence pack is complete.
      </p>
    );
  }

  return (
    <p className="mb-3 rounded border border-mps-line bg-mps-panel px-3 py-2" role="status" data-gate="notEvaluated">
      Evidence gate: <strong>not evaluated</strong>. The investigation ended before the gate was checked.
    </p>
  );
}
