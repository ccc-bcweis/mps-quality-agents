// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company
import { useEffect, useRef, useState } from "react";
import { Link, useParams } from "react-router-dom";
import { api, ApiProblem } from "../api/client";
import { CAPA_STAGE_LABEL, formatStamp } from "../api/labels";
import type { Capa, CapaStage, Incident, Tool } from "../api/types";
import { Explain } from "../components/Explain";
import { HistoryNote, RecordList } from "../components/RecordList";
import { Term } from "../components/Term";
import { btn, panel } from "../components/ui";
import { useConsole, useSetLifecycle } from "../context/ConsoleContext";
import { hiddenMissingTitle, MISSING_CAPA, visibleProblem } from "../history/copy";
import { byNewestTime, toolPlainName } from "../history/order";
import { CAPA_STAGES, nextCapaStage } from "../lifecycle";

export function CapaPage() {
  useSetLifecycle("capa");
  const { capaId } = useParams();
  const { capas, rememberCapa } = useConsole();
  const capasRef = useRef(capas);
  capasRef.current = capas;
  const [capa, setCapa] = useState<Capa | null>(null);
  const [listed, setListed] = useState<Capa[]>([]);
  const [incidents, setIncidents] = useState<Incident[]>([]);
  const [tools, setTools] = useState<Tool[]>([]);
  const [error, setError] = useState<string | null>(null);
  const [missing, setMissing] = useState(false);
  const [notice, setNotice] = useState<string | null>(null);
  const [pending, setPending] = useState(false);
  const [ready, setReady] = useState(false);

  useEffect(() => {
    let cancelled = false;
    Promise.all([api.listCapas(), api.listIncidents(), api.listTools()])
      .then(([nextCapas, nextIncidents, nextTools]) => {
        if (cancelled) return;
        setListed(nextCapas);
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
  }, []);

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

  async function advance(stage: CapaStage) {
    if (!capa) return;
    setPending(true);
    setNotice(null);
    setError(null);
    try {
      const next = await api.advanceCapa(capa.id, { stage });
      setCapa(next);
      rememberCapa(next);
      setNotice(`Moved to ${next.stage}.`);
    } catch (reason: unknown) {
      setError(reason instanceof ApiProblem ? reason.title : "The stage change was rejected.");
    } finally {
      setPending(false);
    }
  }

  const current = capa?.stage ?? "open";
  const currentIndex = CAPA_STAGES.findIndex((item) => item.id === current);
  const upcoming = capa ? nextCapaStage(capa.stage) : null;

  const merged = [...listed, ...capas.filter((item) => !listed.some((row) => row.id === item.id))].sort(
    byNewestTime((item) => item.openedAt),
  );
  const showList = !capa;
  const problem = visibleProblem(error);
  const rows = merged.map((item) => {
    const incident = incidents.find((row) => row.id === item.incidentId);
    return {
      id: item.id,
      status: CAPA_STAGE_LABEL[item.stage],
      toolName: toolPlainName(tools, incident?.toolId),
      toolId: incident?.toolId,
      time: formatStamp(item.openedAt),
      detail: <Link to={`/capas/${item.id}`}>Open</Link>,
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
          <Link to="/approvals">Approval</Link> · mock <Explain text="QMS" spellOut={false} />
        </p>
        <h1 className="text-mps-display font-semibold">
          <Term id="CAPA" spellOut={false} />
        </h1>
        <p className="max-w-3xl text-mps-secondary">
          <Explain text="A CAPA is the record of the fix. Stages after Open move one step at a time. A failed verification does not move backward. The frozen mock rejects advance with 409 until the API implements the transition." />
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
          <Explain text="No CAPA yet. Approve a recommendation to create one. The server list is empty until that token is issued." spellOut={false} />
        </p>
      ) : (
        <>
          <p className="mb-3 rounded border border-mps-ok bg-mps-panel px-3 py-2">
            Approval token consumed (single-use). Token <span className="font-mono">{capa.approvalTokenId}</span>. Action hash <span className="font-mono">{capa.actionHash}</span>.
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
              <Explain text="QMS 500 retry is a connector fault on the mock enterprise API. The console does not call that API. Problem details from the CAPA route show up here." spellOut={false} />
            </p>
          </section>
          <div className="grid gap-3 lg:grid-cols-2">
            <section className={panel}>
              <h2 className="mb-2 text-mps-title font-semibold">Record</h2>
              <p>{capa.summary}</p>
              <p className="mt-2 font-mono text-mps-micro text-mps-muted">Opened {capa.openedAt}</p>
              <p className="font-mono text-mps-micro text-mps-muted">Incident {capa.incidentId}</p>
            </section>
            <section className={panel}>
              <h2 className="mb-2 text-mps-title font-semibold">Audit</h2>
              <p className="font-mono text-mps-micro text-mps-muted">
                Token {capa.approvalTokenId}
                <br />
                actionHash {capa.actionHash}
                <br />
                stage {capa.stage}
              </p>
            </section>
          </div>
        </>
      )}
    </div>
  );
}
