// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company
import { useEffect, useState, type FormEvent } from "react";
import { Link, useNavigate } from "react-router-dom";
import { api, ApiProblem } from "../api/client";
import { CHANNEL_LABEL, STAGE_LABEL, severityTone, severityWord } from "../api/labels";
import type { Incident, Severity, Ticket, Tool } from "../api/types";
import { Chip } from "../components/Chip";
import { Explain } from "../components/Explain";
import { Term } from "../components/Term";
import { btn, btnPrimary, field, panel } from "../components/ui";
import { useSetLifecycle } from "../context/ConsoleContext";
import { lifecycleForIncident } from "../lifecycle";

export function TicketsPage() {
  useSetLifecycle("ticket");
  const navigate = useNavigate();
  const [tickets, setTickets] = useState<Ticket[]>([]);
  const [incidents, setIncidents] = useState<Incident[]>([]);
  const [tools, setTools] = useState<Tool[]>([]);
  const [error, setError] = useState<string | null>(null);
  const [toolId, setToolId] = useState("E-02");
  const [parameter, setParameter] = useState("vibration");
  const [summary, setSummary] = useState("");
  const [severity, setSeverity] = useState<Severity>("medium");
  const [pending, setPending] = useState(false);

  function reload() {
    return Promise.all([api.listTickets(), api.listIncidents(), api.listTools()]).then(([nextTickets, nextIncidents, nextTools]) => {
      setTickets(nextTickets);
      setIncidents(nextIncidents);
      setTools(nextTools);
    });
  }

  useEffect(() => {
    let cancelled = false;
    reload().catch((reason: unknown) => {
      if (!cancelled) setError(reason instanceof ApiProblem ? reason.title : "Tickets did not load.");
    });
    return () => {
      cancelled = true;
    };
  }, []);

  async function onSubmit(event: FormEvent) {
    event.preventDefault();
    setPending(true);
    setError(null);
    try {
      const incident = await api.openIncident({ toolId, parameter, summary, severity });
      await reload();
      const view = lifecycleForIncident(incident.stage);
      if (incident.stage === "ticketOpened") {
        const started = await api.startInvestigation(incident.id);
        navigate(`/investigations/${started.id}`);
        return;
      }
      setPending(false);
      if (view.escalated) navigate(`/investigations/${incident.id}`);
    } catch (reason: unknown) {
      setError(reason instanceof ApiProblem ? reason.title : "The report was not accepted.");
      setPending(false);
    }
  }

  return (
    <div>
      <div className="mb-4">
        <h1 className="text-mps-display font-semibold">Tickets</h1>
        <p className="text-mps-secondary">
          <Explain text="Normalized intake: IoT SPC, operator, QMS NCR, supplier notice, and the fault injector. An open incident on the same tool and parameter is linked." />
        </p>
      </div>
      {error ? <p className="mb-3 text-mps-alarm-text">{error}</p> : null}
      <div className={`${panel} overflow-x-auto`}>
        <table className="w-full text-left">
          <thead>
            <tr className="font-mono text-mps-micro uppercase text-mps-muted">
              <th className="py-1 font-medium">
                <Term id="TKT" spellOut={false} />
              </th>
              <th className="py-1 font-medium">Channel</th>
              <th className="py-1 font-medium">Tool</th>
              <th className="py-1 font-medium">Summary</th>
              <th className="py-1 font-medium">Sev</th>
              <th className="py-1 font-medium">State</th>
              <th className="py-1 font-medium">
                <Term id="INV" spellOut={false} />
              </th>
            </tr>
          </thead>
          <tbody>
            {tickets.map((ticket) => {
              const incident = incidents.find((item) => item.id === ticket.incidentId);
              const stage = incident?.stage;
              return (
                <tr key={ticket.id} className="border-t border-mps-line">
                  <td className="py-1.5 font-mono">
                    <Link to={`/investigations/${ticket.incidentId}`}>{ticket.number}</Link>
                  </td>
                  <td className="py-1.5">{incident ? <Explain text={CHANNEL_LABEL[incident.channel]} spellOut={false} /> : "—"}</td>
                  <td className="py-1.5 font-mono">{ticket.toolId}</td>
                  <td className="py-1.5">{ticket.title}</td>
                  <td className="py-1.5">{incident ? <Chip tone={incident.stage === "closed" ? "ok" : severityTone(incident.severity)}>{incident.stage === "closed" ? "CLOSED" : severityWord(incident.severity)}</Chip> : "—"}</td>
                  <td className="py-1.5">{stage ? STAGE_LABEL[stage] : "—"}</td>
                  <td className="py-1.5">
                    {stage && stage !== "ticketOpened" && stage !== "closed" ? <Link to={`/investigations/${ticket.incidentId}`}>Open</Link> : "—"}
                  </td>
                </tr>
              );
            })}
          </tbody>
        </table>
      </div>
      <form className={`${panel} mt-3 grid gap-3 md:grid-cols-2`} onSubmit={onSubmit}>
        <h2 className="text-mps-title font-semibold md:col-span-2">Operator report</h2>
        <label className="text-mps-label text-mps-muted">
          Tool
          <select className={field} value={toolId} onChange={(event) => setToolId(event.target.value)}>
            {tools.map((tool) => (
              <option key={tool.id} value={tool.id}>
                {tool.id} · {tool.model}
              </option>
            ))}
          </select>
        </label>
        <label className="text-mps-label text-mps-muted">
          Parameter
          <input className={field} value={parameter} onChange={(event) => setParameter(event.target.value)} required />
        </label>
        <label className="text-mps-label text-mps-muted md:col-span-2">
          Summary
          <input className={field} value={summary} onChange={(event) => setSummary(event.target.value)} maxLength={4000} required />
        </label>
        <label className="text-mps-label text-mps-muted">
          Severity
          <select className={field} value={severity} onChange={(event) => setSeverity(event.target.value as Severity)}>
            <option value="low">low</option>
            <option value="medium">medium</option>
            <option value="high">high</option>
            <option value="critical">critical</option>
          </select>
        </label>
        <div className="flex items-end">
          <button className={btnPrimary} type="submit" disabled={pending}>
            {pending ? "Sending…" : "Open ticket"}
          </button>
        </div>
      </form>
      <p className="mt-3">
        <Link className={btn} to="/injector">
          Fault injector
        </Link>
      </p>
    </div>
  );
}
