// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company
import { useEffect, useRef, useState } from "react";
import { Link, useParams } from "react-router-dom";
import { api, ApiProblem } from "../api/client";
import type { Capa, CapaStage } from "../api/types";
import { Explain } from "../components/Explain";
import { Term } from "../components/Term";
import { btn, panel } from "../components/ui";
import { useConsole, useSetLifecycle } from "../context/ConsoleContext";
import { CAPA_STAGES, nextCapaStage } from "../lifecycle";

export function CapaPage() {
  useSetLifecycle("capa");
  const { capaId } = useParams();
  const { capas, rememberCapa } = useConsole();
  const capasRef = useRef(capas);
  capasRef.current = capas;
  const [capa, setCapa] = useState<Capa | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [notice, setNotice] = useState<string | null>(null);
  const [pending, setPending] = useState(false);

  useEffect(() => {
    let cancelled = false;
    const remembered = capaId ? capasRef.current.find((item) => item.id === capaId) ?? null : capasRef.current[0] ?? null;
    if (!capaId) {
      setCapa(remembered);
      if (!remembered) setError(null);
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
        }
      })
      .catch((reason: unknown) => {
        if (cancelled) return;
        if (remembered) {
          setCapa(remembered);
          setError(null);
          return;
        }
        setError(reason instanceof ApiProblem ? reason.title : "CAPA was not found.");
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
      {error ? (
        <p className="mb-3 text-mps-alarm-text" role="alert">
          <Explain text={error} spellOut={false} />
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
