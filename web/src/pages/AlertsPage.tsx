// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company
import { useEffect, useState } from "react";
import { Link, useParams, useSearchParams } from "react-router-dom";
import { api, ApiProblem } from "../api/client";
import { formatStamp, ruleLabel, severityTone, severityWord } from "../api/labels";
import type { Alert, Incident, Ticket, Tool, ManualReading } from "../api/types";
import { CaseBriefs } from "../components/CaseBriefs";
import { Chip } from "../components/Chip";
import { Explain } from "../components/Explain";
import { HistoryNote, RecordList } from "../components/RecordList";
import { panel, btn, btnSm } from "../components/ui";
import { useConsole, useSetLifecycle } from "../context/ConsoleContext";
import { MISSING_ALERT, visibleProblem } from "../history/copy";
import { byNewestTime, toolPlainName } from "../history/order";
import { useSite } from "../context/SiteContext";
import { toolInPlant } from "../sites/selection";

export function AlertsPage() {
  useSetLifecycle("alert");
  const { alertId } = useParams();
  const [params] = useSearchParams();
  const { siteId } = useSite();
  const { localAlerts } = useConsole();
  const [alerts, setAlerts] = useState<Alert[]>([]);
  const [tickets, setTickets] = useState<Ticket[]>([]);
  const [incidents, setIncidents] = useState<Incident[]>([]);
  const [tools, setTools] = useState<Tool[]>([]);
  const [manual, setManual] = useState<ManualReading[]>([]);
  const [error, setError] = useState<string | null>(null);
  const [ready, setReady] = useState(false);
  const injected = params.get("injected") === "1";

  useEffect(() => {
    let cancelled = false;
    setReady(false);
    Promise.all([
      api.listAlerts(undefined, siteId),
      api.listTickets(siteId),
      api.listIncidents(siteId),
      api.listTools(),
      api.listManualReadings().catch(() => [] as ManualReading[]),
    ])
      .then(([nextAlerts, nextTickets, nextIncidents, nextTools, nextManual]) => {
        if (cancelled) return;
        setManual(nextManual);
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
  }, [injected, localAlerts, siteId]);

  const merged = [...localAlerts, ...alerts]
    .filter((alert, index, all) => all.findIndex((item) => item.correlationId === alert.correlationId) === index)
    .filter((alert) => toolInPlant(siteId, alert.toolId))
    .sort(byNewestTime((alert) => alert.raisedAt));
  const fresh = merged.filter((alert) => alert.correlationId.startsWith("corr-inject") || alert.correlationId.startsWith("corr-scenario"));
  const selected = alertId ? merged.find((alert) => alert.id === alertId) : undefined;
  const missing = Boolean(alertId) && ready && !selected;
  const problem = visibleProblem(error);

  const rows = merged.map((alert) => {
    const incident = incidents.find((item) => item.correlationId === alert.correlationId);
    const matched = tickets.find((item) => item.incidentId === incident?.id);
    return {
      id: alert.id,
      status: (
        <>
          <Chip tone={severityTone(alert.severity)}>{severityWord(alert.severity)}</Chip>
          {alert.status === "cleared" ? <> <Chip tone="nominal">Cleared</Chip></> : null}
        </>
      ),
      toolName: toolPlainName(tools, alert.toolId),
      toolId: alert.toolId,
      time: formatStamp(alert.raisedAt),
      detail: (
        <>
          <Explain text={ruleLabel(alert.rule)} />
          <div className="mt-1">
            {matched ? <Link className={btnSm} to={`/tickets/${matched.id}`}>{matched.number}</Link> : "—"}
            {" · "}
            <Link className={btnSm} to={`/alerts/${alert.id}`}>Open</Link>
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
      {problem ? <p className="mb-3 text-mps-alarm-text">{problem}</p> : null}
      {missing ? <HistoryNote text={MISSING_ALERT} /> : null}
      {selected ? (
        <section className={`${panel} mb-3`}>
          <h2 className="mb-2 text-mps-title font-semibold">Alert</h2>
          <p>
            <Chip tone={severityTone(selected.severity)}>{severityWord(selected.severity)}</Chip> · {toolPlainName(tools, selected.toolId)} · {formatStamp(selected.raisedAt)}
            {" · "}
            {selected.status === "cleared"
              ? `Cleared ${selected.clearedAt ? formatStamp(selected.clearedAt) : ""}`.trim()
              : "Active until 8 readings in a row are back in control"}
          </p>
          <p className="mt-2">
            <Explain text={ruleLabel(selected.rule)} /> · {selected.parameter}
          </p>
          {(() => {
            const reading = manual.find((row) => row.alertId === selected.id);
            if (!reading) return null;
            return (
              <p className="mt-2" data-alert-source="true">
                Source: {reading.source} Observed {formatStamp(reading.observedAt)}
                {reading.printedObservedAt ? ` (printed ${formatStamp(reading.printedObservedAt)}, set to now by the user)` : ""}.{" "}
                {reading.fileUrl ? (
                  <a className={btnSm} href={reading.fileUrl} target="_blank" rel="noreferrer">
                    Open printout
                  </a>
                ) : null}
              </p>
            );
          })()}
          <p className="mt-2">
            <Link className={btn} to="/alerts">All alerts</Link>
          </p>
        </section>
      ) : null}
      {!alertId || missing ? <RecordList label="Alerts" rows={rows} empty="No alerts yet." /> : null}
      <CaseBriefs
        incidentIds={incidents.filter((incident) => merged.some((alert) => alert.correlationId === incident.correlationId)).map((incident) => incident.id)}
        intro="Evidence linked to these alerts"
      />
    </div>
  );
}
