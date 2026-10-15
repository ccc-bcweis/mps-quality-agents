// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company
import { useEffect, useState } from "react";
import { api, ApiProblem } from "../api/client";
import { formatStamp } from "../api/labels";
import type { AuditLog } from "../api/types";
import { panel } from "../components/ui";

const KIND_LABEL: Record<string, string> = {
  fault: "Fault",
  alert: "Alert",
  ticket: "Ticket",
  incident: "Incident",
  investigation: "Investigation",
  approval: "Approval",
  capa: "Corrective action",
};

export function AuditPage() {
  const [log, setLog] = useState<AuditLog | null>(null);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    let cancelled = false;
    api
      .getAuditLog()
      .then((next) => {
        if (!cancelled) setLog(next);
      })
      .catch((reason: unknown) => {
        if (!cancelled) setError(reason instanceof ApiProblem ? reason.title : "The audit log did not load.");
      });
    return () => {
      cancelled = true;
    };
  }, []);

  const entries = log?.entries ?? [];
  const traces = log?.traces ?? [];

  return (
    <div>
      <h1 className="text-mps-display font-semibold">Audit log</h1>
      <p className="max-w-3xl text-mps-secondary">
        Presenter view of the recorded steps and the correlation traces that tie a fault to its ticket, investigation, and approval. The console does not show these ids.
      </p>
      {error ? <p className="mt-3 text-mps-alarm-text">{error}</p> : null}
      {!log && !error ? <p className="mt-3 text-mps-muted">Loading the audit log…</p> : null}
      {log && entries.length === 0 ? (
        <p className="mt-3 text-mps-muted">No audit events yet. They show up after a scenario or a manual injection.</p>
      ) : null}
      {entries.length > 0 ? (
        <section className={`${panel} mt-4 overflow-x-auto`}>
          <h2 className="mb-2 text-mps-title font-semibold">Trail</h2>
          <table className="w-full text-left">
            <thead>
              <tr className="font-mono text-mps-micro uppercase text-mps-muted">
                <th className="py-1 font-medium">When</th>
                <th className="py-1 font-medium">Step</th>
                <th className="py-1 font-medium">What happened</th>
                <th className="py-1 font-medium">Tool</th>
              </tr>
            </thead>
            <tbody>
              {entries.map((entry) => (
                <tr key={`${entry.kind}-${entry.recordId ?? entry.at}`} className="border-t border-mps-line">
                  <td className="py-1.5 whitespace-nowrap font-mono text-mps-micro">{formatStamp(entry.at)}</td>
                  <td className="py-1.5 whitespace-nowrap">{KIND_LABEL[entry.kind] ?? entry.kind}</td>
                  <td className="py-1.5">{entry.summary}</td>
                  <td className="py-1.5 font-mono">{entry.toolId ?? "—"}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </section>
      ) : null}
      {traces.length > 0 ? (
        <section className="mt-4 space-y-3">
          <h2 className="text-mps-title font-semibold">Correlation traces</h2>
          {traces.map((trace) => (
            <article key={trace.correlationId} className={panel}>
              <h3 className="font-mono text-mps-micro text-mps-nominal">{trace.correlationId}</h3>
              <ol className="mt-2 space-y-1">
                {trace.events.map((event) => (
                  <li key={`${trace.correlationId}-${event.kind}-${event.recordId ?? event.at}`}>
                    <span className="font-mono text-mps-micro text-mps-muted">{formatStamp(event.at)}</span> {event.summary}
                  </li>
                ))}
              </ol>
            </article>
          ))}
        </section>
      ) : null}
    </div>
  );
}
