// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company
import { useEffect, useState } from "react";
import { Link, useSearchParams } from "react-router-dom";
import { api, ApiProblem } from "../api/client";
import { formatStamp, ruleLabel, severityTone, severityWord } from "../api/labels";
import type { Alert, Incident, Ticket } from "../api/types";
import { Chip } from "../components/Chip";
import { btn, panel } from "../components/ui";
import { useConsole, useSetLifecycle } from "../context/ConsoleContext";

export function AlertsPage() {
  useSetLifecycle("alert");
  const [params] = useSearchParams();
  const { localAlerts } = useConsole();
  const [alerts, setAlerts] = useState<Alert[]>([]);
  const [tickets, setTickets] = useState<Ticket[]>([]);
  const [incidents, setIncidents] = useState<Incident[]>([]);
  const [error, setError] = useState<string | null>(null);
  const injected = params.get("injected") === "1";

  useEffect(() => {
    let cancelled = false;
    Promise.all([api.listAlerts(), api.listTickets(), api.listIncidents()])
      .then(([nextAlerts, nextTickets, nextIncidents]) => {
        if (cancelled) return;
        setAlerts(nextAlerts);
        setTickets(nextTickets);
        setIncidents(nextIncidents);
      })
      .catch((reason: unknown) => {
        if (!cancelled) setError(reason instanceof ApiProblem ? reason.title : "Alerts did not load.");
      });
    return () => {
      cancelled = true;
    };
  }, [injected, localAlerts]);

  const merged = [...localAlerts, ...alerts].filter(
    (alert, index, all) => all.findIndex((item) => item.correlationId === alert.correlationId) === index,
  );
  const fresh = merged.filter((alert) => alert.correlationId.startsWith("corr-inject"));

  return (
    <div>
      <div className="mb-4 flex flex-wrap items-end justify-between gap-3">
        <div>
          <h1 className="text-mps-display font-semibold">Alerts</h1>
          <p className="text-mps-secondary">Spec-window violations and Western Electric rules. Debounced per tool and parameter. The rules engine is deterministic.</p>
        </div>
        <Link className={btn} to="/injector">
          Inject a fault
        </Link>
      </div>
      {injected ? (
        <p className="mb-3 rounded border border-mps-warn bg-mps-panel px-3 py-2 text-mps-secondary" role="status">
          Injected fault accepted. {fresh.length === 0 ? "Waiting for the alert." : `${fresh.length} new alert${fresh.length === 1 ? "" : "s"} on this list.`}
        </p>
      ) : null}
      {error ? <p className="mb-3 text-mps-alarm-text">{error}</p> : null}
      <div className={panel}>
        <h2 className="mb-2 text-mps-title font-semibold">
          Active <span className="float-right font-mono text-mps-micro font-normal text-mps-nominal">rules engine · not a model</span>
        </h2>
        <div className="overflow-x-auto">
          <table className="w-full text-left">
            <thead>
              <tr className="font-mono text-mps-micro uppercase text-mps-muted">
                <th className="py-1 font-medium">Tool</th>
                <th className="py-1 font-medium">Parameter</th>
                <th className="py-1 font-medium">Rule</th>
                <th className="py-1 font-medium">Severity</th>
                <th className="py-1 font-medium">Raised</th>
                <th className="py-1 font-medium">Correlation</th>
                <th className="py-1 font-medium">Ticket</th>
              </tr>
            </thead>
            <tbody>
              {merged.map((alert) => {
                const incident = incidents.find((item) => item.correlationId === alert.correlationId);
                const matched = tickets.find((item) => item.incidentId === incident?.id);
                return (
                  <tr key={alert.id} className="border-t border-mps-line">
                    <td className="py-1.5 font-mono">
                      <Link to={`/tools/${alert.toolId}`}>{alert.toolId}</Link>
                    </td>
                    <td className="py-1.5 font-mono">{alert.parameter}</td>
                    <td className="py-1.5">{ruleLabel(alert.rule)}</td>
                    <td className="py-1.5">
                      <Chip tone={severityTone(alert.severity)}>{severityWord(alert.severity)}</Chip>
                    </td>
                    <td className="py-1.5 font-mono text-mps-micro">{formatStamp(alert.raisedAt)}</td>
                    <td className="py-1.5 font-mono text-mps-micro">{alert.correlationId}</td>
                    <td className="py-1.5 font-mono">{matched ? <Link to="/tickets">{matched.number}</Link> : "—"}</td>
                  </tr>
                );
              })}
            </tbody>
          </table>
        </div>
      </div>
    </div>
  );
}
