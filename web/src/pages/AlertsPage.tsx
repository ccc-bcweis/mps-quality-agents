// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company
import { useEffect, useState } from "react";
import { Link, useParams, useSearchParams } from "react-router-dom";
import { api, ApiProblem } from "../api/client";
import { formatStamp, ruleLabel, severityTone, severityWord } from "../api/labels";
import type { Alert, Incident, Ticket, Tool } from "../api/types";
import { Chip } from "../components/Chip";
import { Explain } from "../components/Explain";
import { HistoryNote, RecordList } from "../components/RecordList";
import { panel } from "../components/ui";
import { useConsole, useSetLifecycle } from "../context/ConsoleContext";
import { MISSING_ALERT } from "../history/copy";
import { byNewestTime, toolPlainName } from "../history/order";

export function AlertsPage() {
  useSetLifecycle("alert");
  const { alertId } = useParams();
  const [params] = useSearchParams();
  const { localAlerts } = useConsole();
  const [alerts, setAlerts] = useState<Alert[]>([]);
  const [tickets, setTickets] = useState<Ticket[]>([]);
  const [incidents, setIncidents] = useState<Incident[]>([]);
  const [tools, setTools] = useState<Tool[]>([]);
  const [error, setError] = useState<string | null>(null);
  const [ready, setReady] = useState(false);
  const injected = params.get("injected") === "1";

  useEffect(() => {
    let cancelled = false;
    setReady(false);
    Promise.all([api.listAlerts(), api.listTickets(), api.listIncidents(), api.listTools()])
      .then(([nextAlerts, nextTickets, nextIncidents, nextTools]) => {
        if (cancelled) return;
        setAlerts(nextAlerts);
        setTickets(nextTickets);
        setIncidents(nextIncidents);
        setTools(nextTools);
      })
      .catch((reason: unknown) => {
        if (!cancelled) setError(reason instanceof ApiProblem ? reason.title : "Alerts did not load.");
      })
      .finally(() => {
        if (!cancelled) setReady(true);
      });
    return () => {
      cancelled = true;
    };
  }, [injected, localAlerts]);

  const merged = [...localAlerts, ...alerts]
    .filter((alert, index, all) => all.findIndex((item) => item.correlationId === alert.correlationId) === index)
    .sort(byNewestTime((alert) => alert.raisedAt));
  const fresh = merged.filter((alert) => alert.correlationId.startsWith("corr-inject") || alert.correlationId.startsWith("corr-scenario"));
  const selected = alertId ? merged.find((alert) => alert.id === alertId) : undefined;
  const missing = Boolean(alertId) && ready && !selected;

  const rows = merged.map((alert) => {
    const incident = incidents.find((item) => item.correlationId === alert.correlationId);
    const matched = tickets.find((item) => item.incidentId === incident?.id);
    return {
      id: alert.id,
      status: <Chip tone={severityTone(alert.severity)}>{severityWord(alert.severity)}</Chip>,
      toolName: toolPlainName(tools, alert.toolId),
      toolId: alert.toolId,
      time: formatStamp(alert.raisedAt),
      detail: (
        <>
          <div className="font-mono text-mps-micro text-mps-muted">{alert.correlationId}</div>
          <Explain text={ruleLabel(alert.rule)} />
          <div className="mt-1">
            {matched ? <Link to="/tickets">{matched.number}</Link> : "—"}
            {" · "}
            <Link to={`/alerts/${alert.id}`}>Open</Link>
          </div>
        </>
      ),
    };
  });

  return (
    <div>
      <div className="mb-4 flex flex-wrap items-end justify-between gap-3">
        <div>
          <h1 className="text-mps-display font-semibold">Alerts</h1>
          <p className="text-mps-secondary">Spec-window violations and Western Electric rules. Debounced per tool and parameter. The rules engine is deterministic.</p>
        </div>
      </div>
      {injected ? (
        <p className="mb-3 rounded border border-mps-warn bg-mps-panel px-3 py-2 text-mps-secondary" role="status">
          Injected fault accepted. {fresh.length === 0 ? "Waiting for the alert." : `${fresh.length} new alert${fresh.length === 1 ? "" : "s"} on this list.`}
        </p>
      ) : null}
      {error ? <p className="mb-3 text-mps-alarm-text">{error}</p> : null}
      {missing ? <HistoryNote text={MISSING_ALERT} /> : null}
      {selected ? (
        <section className={`${panel} mb-3`}>
          <h2 className="mb-2 text-mps-title font-semibold">Alert</h2>
          <p>
            <Chip tone={severityTone(selected.severity)}>{severityWord(selected.severity)}</Chip> · {toolPlainName(tools, selected.toolId)} · {formatStamp(selected.raisedAt)}
          </p>
          <p className="mt-2">
            <Explain text={ruleLabel(selected.rule)} /> · {selected.parameter}
          </p>
          <p className="mt-2 font-mono text-mps-micro text-mps-muted">{selected.correlationId}</p>
          <p className="mt-2">
            <Link to="/alerts">All alerts</Link>
          </p>
        </section>
      ) : null}
      {!alertId || missing ? <RecordList label="Alerts" rows={rows} empty="No alerts yet." /> : null}
    </div>
  );
}
