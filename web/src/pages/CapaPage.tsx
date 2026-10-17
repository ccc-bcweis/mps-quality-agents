// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company
import { useEffect, useRef, useState } from "react";
import { Link, useParams } from "react-router-dom";
import { api, ApiProblem } from "../api/client";
import { approvalSentence, CAPA_STAGE_LABEL, formatStamp } from "../api/labels";
import type { Capa, CapaStage, Incident, Investigation, Tool } from "../api/types";
import { AgentTimeline } from "../components/AgentTimeline";
import { EvidencePack } from "../components/EvidencePack";
import { Explain } from "../components/Explain";
import { HistoryNote, RecordList } from "../components/RecordList";
import { Term } from "../components/Term";
import { btn, panel, btnSm, btnPrimary } from "../components/ui";
import { useConsole, useSetLifecycle } from "../context/ConsoleContext";
import { hiddenMissingTitle, MISSING_CAPA, visibleProblem } from "../history/copy";
import { byNewestTime, toolPlainName } from "../history/order";
import { useSite } from "../context/SiteContext";
import { toolInPlant } from "../sites/selection";
import { splitBySite } from "../sites/splitBySite";
import { formatLocalWhen } from "../investigation/format";
import { CAPA_STAGES, nextCapaStage } from "../lifecycle";

export function CapaPage() {
  useSetLifecycle("capa");
  const { capaId } = useParams();
  const { siteId } = useSite();
  const { capas, rememberCapa, syncCapas } = useConsole();
  const capasRef = useRef(capas);
  capasRef.current = capas;
  const [capa, setCapa] = useState<Capa | null>(null);
  const [listed, setListed] = useState<Capa[]>([]);
  const [serverListed, setServerListed] = useState(false);
  const [incidents, setIncidents] = useState<Incident[]>([]);
  const [tools, setTools] = useState<Tool[]>([]);
  const [homeTool, setHomeTool] = useState<string | null>(null);
  const [investigation, setInvestigation] = useState<Investigation | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [missing, setMissing] = useState(false);
  const [notice, setNotice] = useState<string | null>(null);
  const [pending, setPending] = useState(false);
  const [ready, setReady] = useState(false);
  const [capaTools, setCapaTools] = useState<Record<string, string>>({});

  useEffect(() => {
    let cancelled = false;
    Promise.all([api.listCapas(), api.listIncidents(), api.listTools()])
      .then(([nextCapas, nextIncidents, nextTools]) => {
        if (cancelled) return;
        setListed(nextCapas);
        setServerListed(true);
        syncCapas(nextCapas.map((item) => item.id));
        setIncidents(nextIncidents);
        setTools(nextTools);
      })
      .catch((reason: unknown) => {
        if (!cancelled) setError(reason instanceof ApiProblem ? reason.title : "Corrective actions did not load.");
      })
      .finally(() => {
        if (!cancelled) setReady(true);
      });
    return () => {
      cancelled = true;
    };
  }, [syncCapas]);

  useEffect(() => {
    let cancelled = false;
    const remembered = capaId ? capasRef.current.find((item) => item.id === capaId) ?? null : null;
    setMissing(false);
    if (!capaId) {
      setCapa(null);
      setError(null);
      return () => {
        cancelled = true;
      };
    }
    api
      .getCapa(capaId)
      .then((next) => {
        if (!cancelled) {
          setCapa(next);
          rememberCapa(next);
          setMissing(false);
        }
      })
      .catch((reason: unknown) => {
        if (cancelled) return;
        if (remembered) {
          setCapa(remembered);
          setError(null);
          setMissing(false);
          return;
        }
        setCapa(null);
        if (reason instanceof ApiProblem && (reason.status === 404 || hiddenMissingTitle(reason.title))) {
          setMissing(true);
          setError(null);
          return;
        }
        setMissing(false);
        setError(reason instanceof ApiProblem ? reason.title : "The corrective action did not load.");
      });
    return () => {
      cancelled = true;
    };
  }, [capaId, rememberCapa]);

  useEffect(() => {
    const unknown = [...listed, ...capas]
      .map((item) => item.incidentId)
      .filter((id, index, all) => all.indexOf(id) === index && !incidents.some((row) => row.id === id) && !(id in capaTools));
    if (unknown.length === 0) return undefined;
    let cancelled = false;
    Promise.all(unknown.map((id) => api.getIncident(id).then((incident) => [id, incident.toolId] as const).catch(() => null))).then((pairs) => {
      if (cancelled) return;
      setCapaTools((prev) => {
        const next = { ...prev };
        for (const pair of pairs) if (pair) next[pair[0]] = pair[1];
        return next;
      });
    });
    return () => {
      cancelled = true;
    };
  }, [listed, capas, incidents, capaTools]);

  useEffect(() => {
    if (!capa) {
      setHomeTool(null);
      return undefined;
    }
    let cancelled = false;
    api
      .getIncident(capa.incidentId)
      .then((incident) => {
        if (!cancelled) setHomeTool(incident.toolId);
      })
      .catch(() => {
        if (!cancelled) setHomeTool(null);
      });
    return () => {
      cancelled = true;
    };
  }, [capa]);

  const incidentId = capa?.incidentId ?? "";
  useEffect(() => {
    if (!incidentId) {
      setInvestigation(null);
      return undefined;
    }
    let cancelled = false;
    api
      .getLatestInvestigation(incidentId)
      .then((next) => {
        if (!cancelled) setInvestigation(next);
      })
      .catch(() => {
        if (!cancelled) setInvestigation(null);
      });
    return () => {
      cancelled = true;
    };
  }, [incidentId]);

  async function advance(stage: CapaStage) {
    if (!capa) return;
    setPending(true);
    setNotice(null);
    setError(null);
    try {
      const next = await api.advanceCapa(capa.id, { stage });
      setCapa(next);
      rememberCapa(next);
      const label = CAPA_STAGES.find((item) => item.id === next.stage)?.label ?? next.stage;
      setNotice(`Moved to ${label}.`);
    } catch (reason: unknown) {
      setError(reason instanceof ApiProblem ? reason.title : "The stage change was rejected.");
    } finally {
      setPending(false);
    }
  }

  const current = capa?.stage ?? "open";
  const currentIndex = CAPA_STAGES.findIndex((item) => item.id === current);
  const upcoming = capa ? nextCapaStage(capa.stage) : null;

  // The server list is the record. Tab memory only fills in while the server list has not loaded.
  const merged = (serverListed ? listed : [...listed, ...capas.filter((item) => !listed.some((row) => row.id === item.id))]).sort(
    byNewestTime((item) => item.openedAt),
  );
  const showList = !capa;
  const toolFor = (item: Capa) => item.toolId ?? incidents.find((row) => row.id === item.incidentId)?.toolId ?? capaTools[item.incidentId];
  const { visible, elsewhere } = splitBySite(merged, siteId, toolFor);
  const problem = visibleProblem(error);
  const rows = visible.map((item) => {
    const toolId = toolFor(item);
    return {
      id: item.id,
      status: CAPA_STAGE_LABEL[item.stage],
      toolName: toolPlainName(tools, toolId),
      toolId,
      time: formatStamp(item.openedAt),
      detail: (
        <>
          <div>{item.summary}</div>
          <Link className={btnSm} to={`/capas/${item.id}`}>Open</Link>
        </>
      ),
    };
  });

  if (showList) {
    return (
      <div>
        <div className="mb-4">
          <h1 className="text-mps-display font-semibold">
            <Term id="CAPA" spellOut={false} />
          </h1>
          <p className="max-w-3xl text-mps-secondary">Past corrective and preventive actions, newest first.</p>
        </div>
        {missing && ready ? <HistoryNote text={MISSING_CAPA} /> : null}
        {elsewhere > 0 ? (
          <HistoryNote text={`${elsewhere} corrective action${elsewhere === 1 ? " is" : "s are"} at another plant. Choose All sites to see ${elsewhere === 1 ? "it" : "them"}.`} />
        ) : null}
        {problem ? (
          <p className="mb-3 text-mps-alarm-text" role="alert">
            <Explain text={problem} spellOut={false} />
          </p>
        ) : null}
        {rows.length === 0 ? (
          <p className="text-mps-muted">
            <Explain text="No CAPA yet. Approve a recommendation to create one. The server list is empty until that token is issued." spellOut={false} />
          </p>
        ) : (
          <RecordList label="Corrective actions" rows={rows} empty="No corrective actions yet." />
        )}
      </div>
    );
  }

  return (
    <div>
      <div className="mb-4">
        <p className="font-mono text-mps-micro uppercase tracking-wider text-mps-muted">
          <Link className={btnSm} to="/investigations">Investigations</Link> · <Explain text="QMS" spellOut={false} />
        </p>
        <h1 className="text-mps-display font-semibold">
          <Term id="CAPA" spellOut={false} />
        </h1>
        <p className="max-w-3xl text-mps-secondary">
          <Explain text="A CAPA is the record of the fix. Stages after Open move one step at a time. A failed verification does not move backward. An invalid advance is rejected with 409." />
        </p>
      </div>
      {problem ? (
        <p className="mb-3 text-mps-alarm-text" role="alert">
          <Explain text={problem} spellOut={false} />
        </p>
      ) : null}
      {notice ? <p className="mb-3 text-mps-ok">{notice}</p> : null}
      {!capa ? (
        <p className="text-mps-muted">
          <Explain text="No CAPA yet. Approve a recommendation to create one." spellOut={false} />
        </p>
      ) : homeTool && !toolInPlant(siteId, homeTool) ? (
        <p>This corrective action is at another plant. Choose All sites to open it.</p>
      ) : (
        <>
          <p className="mb-3 rounded border border-mps-ok bg-mps-panel px-3 py-2" role="status">
            {capa.approvedBy && capa.approvedAt ? approvalSentence(capa.approvedBy, capa.approvedAt) : "Approved."}
          </p>
          <section className={`${panel} mb-3`}>
            <h2 className="mb-2 text-mps-title font-semibold">
              <Explain text="CAPA stages" spellOut={false} />
            </h2>
            <ol aria-label="CAPA stages" className="m-0 flex list-none flex-wrap gap-0 p-0">
              {CAPA_STAGES.map((item, index) => {
                const state = index < currentIndex ? "done" : index === currentIndex ? "current" : "todo";
                return (
                  <li key={item.id} aria-current={state === "current" ? "step" : undefined} className={`mr-3 font-mono text-mps-micro uppercase ${state === "current" ? "text-mps-ok" : state === "done" ? "text-mps-secondary" : "text-mps-muted"}`}>
                    {item.label}
                  </li>
                );
              })}
            </ol>
            <div className="mt-3 flex flex-wrap gap-2">
              <button type="button" className={btn} disabled={!upcoming || pending} onClick={() => upcoming && void advance(upcoming)}>
                Advance stage
              </button>
            </div>
            <p className="mt-3 text-mps-micro text-mps-muted">
              <Explain text="QMS 500 retry is a connector fault on the enterprise QMS API. The console does not call that API. Problem details from the CAPA route show up here." spellOut={false} />
            </p>
          </section>
          <div className="grid gap-3 lg:grid-cols-2">
            <section className={panel}>
              <h2 className="mb-2 text-mps-title font-semibold">Record</h2>
              <p>{capa.summary}</p>
              <p className="mt-2 text-mps-secondary">Opened {formatLocalWhen(capa.openedAt)}</p>
              <p className="mt-2">
                <Link className={btnPrimary} to={`/investigations/incident/${capa.incidentId}`} title={capa.incidentId}>
                  Open the investigation
                </Link>
              </p>
              <p className="mt-2 text-mps-secondary">
                {capa.approvedBy && capa.approvedAt ? approvalSentence(capa.approvedBy, capa.approvedAt) : "Approved."}
              </p>
              <p className="mt-2" data-testid="capa-note">
                <span className="text-mps-secondary">Approver's note: </span>
                {capa.note?.trim() ? capa.note : "No note was written."}
              </p>
              <p className="mt-2 text-mps-secondary">Stage {CAPA_STAGES.find((item) => item.id === capa.stage)?.label ?? capa.stage}</p>
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
        </>
      )}
    </div>
  );
}

