// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company
import { useEffect, useState } from "react";
import { api, ApiProblem } from "../api/client";
import type { BusinessRule, Incident, ProcessMap, Ticket } from "../api/types";
import { btn, btnPrimary, panel } from "../components/ui";
import { useSetLifecycle } from "../context/ConsoleContext";
import { useSite } from "../context/SiteContext";
import { visibleProblem } from "../history/copy";
import { ProcessFlow } from "../rules/ProcessFlow";

type MapState = ProcessMap["state"];

export function RulesProcessPage() {
  useSetLifecycle("ticket");
  const [rules, setRules] = useState<BusinessRule[]>([]);
  const [maps, setMaps] = useState<ProcessMap[]>([]);
  const [tickets, setTickets] = useState<Ticket[]>([]);
  const [incidents, setIncidents] = useState<Incident[]>([]);
  const { siteId } = useSite();
  const [error, setError] = useState<string | null>(null);
  const [ready, setReady] = useState(false);
  const [mapState, setMapState] = useState<MapState>("current");
  const [ticketId, setTicketId] = useState<string | null>(null);

  useEffect(() => {
    let cancelled = false;
    Promise.all([api.businessRules(), api.processMaps(), api.listTickets(siteId), api.listIncidents(siteId)])
      .then(([nextRules, nextMaps, nextTickets, nextIncidents]) => {
        if (cancelled) return;
        setRules(nextRules);
        setMaps(nextMaps);
        setTickets(nextTickets);
        setIncidents(nextIncidents);
      })
      .catch((reason: unknown) => {
        if (!cancelled) setError(reason instanceof ApiProblem ? reason.title : "The rules did not load.");
      })
      .finally(() => {
        if (!cancelled) setReady(true);
      });
    return () => {
      cancelled = true;
    };
  }, [siteId]);

  const map = maps.find((item) => item.state === mapState) ?? null;
  const ticket = tickets.find((item) => item.id === ticketId) ?? null;
  const stage = incidents.find((item) => item.id === ticket?.incidentId)?.stage;
  const highlighted = new Set(
    (map?.steps ?? []).filter((step) => stage !== undefined && step.highlightStages.includes(stage)).map((step) => step.id),
  );

  return (
    <div>
      <h1 className="text-mps-display font-semibold">Rules & Process</h1>
      <p className="max-w-3xl text-mps-secondary">
        These rules decide when a ticket opens, which queue owns it, and when a person has to review the case. The maps
        show how a quality issue moves today and how it moves when the rules run.
      </p>
      {visibleProblem(error) ? <p className="mt-3 text-mps-alarm-text">{visibleProblem(error)}</p> : null}
      {!ready && !error ? <p className="mt-3 text-mps-muted">Loading rules…</p> : null}

      <section aria-label="Business rules" className={`${panel} mt-4`}>
        <h2 className="text-mps-title font-semibold">Rules</h2>
        <table className="mt-3 w-full table-fixed text-left">
          <thead>
            <tr className="text-mps-micro text-mps-muted">
              <th className="w-1/3 py-2 pr-3 font-semibold">When</th>
              <th className="w-1/3 py-2 pr-3 font-semibold">Then</th>
              <th className="w-1/3 py-2 font-semibold">Why</th>
            </tr>
          </thead>
          <tbody>
            {rules.map((rule) => (
              <tr key={rule.id} className="border-t border-mps-line align-top">
                <td className="break-words py-2 pr-3">{rule.whenPlain}</td>
                <td className="break-words py-2 pr-3">{rule.thenPlain}</td>
                <td className="break-words py-2">{rule.why}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </section>

      <section aria-label="Process maps" className={`${panel} mt-4`}>
        <h2 className="text-mps-title font-semibold">Process maps</h2>
        <div className="mt-3 flex flex-wrap gap-2">
          <button
            type="button"
            className={mapState === "current" ? btnPrimary : btn}
            aria-pressed={mapState === "current"}
            onClick={() => setMapState("current")}
          >
            Current state
          </button>
          <button
            type="button"
            className={mapState === "future" ? btnPrimary : btn}
            aria-pressed={mapState === "future"}
            onClick={() => setMapState("future")}
          >
            Future state
          </button>
        </div>
        {map ? (
          <>
            <h3 className="mt-4 text-mps-body font-semibold">{map.name}</h3>
            <div className="mt-3">
              <ProcessFlow steps={map.steps} highlighted={highlighted} />
            </div>
            <h3 className="mt-4 text-mps-body font-semibold">Moves</h3>
            <ul className="mt-2 space-y-1">
              {map.transitions.map((transition) => (
                <li key={`${transition.from}-${transition.to}`} className="break-words">
                  {transition.name}
                </li>
              ))}
            </ul>
          </>
        ) : ready ? (
          <p className="mt-3 text-mps-muted">The process map did not load.</p>
        ) : null}
        <h3 className="mt-4 text-mps-body font-semibold">Tickets</h3>
        {tickets.length === 0 ? (
          <p className="mt-2 text-mps-muted">No tickets yet.</p>
        ) : (
          <div role="group" aria-label="Tickets" className="mt-2 flex flex-col items-start gap-2">
            {tickets.map((item) => (
              <button
                key={item.id}
                type="button"
                className={`${item.id === ticketId ? btnPrimary : btn} h-auto max-w-full whitespace-normal break-words text-left`}
                aria-pressed={item.id === ticketId}
                onClick={() => setTicketId(item.id)}
              >
                {item.title}
              </button>
            ))}
          </div>
        )}
      </section>
    </div>
  );
}
