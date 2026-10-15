// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company
import { useCallback, useEffect, useState, type FormEvent } from "react";
import { Link, useNavigate, useParams } from "react-router-dom";
import { api, ApiProblem } from "../api/client";
import { formatStamp, STAGE_LABEL, severityTone, severityWord } from "../api/labels";
import type { Incident, Severity, Ticket, Tool } from "../api/types";
import { CaseBriefs } from "../components/CaseBriefs";
import { Chip } from "../components/Chip";
import { Explain } from "../components/Explain";
import { HistoryNote, RecordList } from "../components/RecordList";
import { btnPrimary, field, panel } from "../components/ui";
import { useSetLifecycle } from "../context/ConsoleContext";
import { MISSING_TICKET, visibleProblem } from "../history/copy";
import { byNewestTime, toolPlainName } from "../history/order";
import { plainTool } from "../investigation/format";
import { useSite } from "../context/SiteContext";
import { pathForIncident, pathForTicket } from "../investigation/ticketRoute";
import { toolInPlant } from "../sites/selection";
import { lifecycleForIncident } from "../lifecycle";

export function TicketsPage() {
  useSetLifecycle("ticket");
  const navigate = useNavigate();
  const { ticketId } = useParams();
  const { siteId } = useSite();
  const [tickets, setTickets] = useState<Ticket[]>([]);
  const [incidents, setIncidents] = useState<Incident[]>([]);
  const [tools, setTools] = useState<Tool[]>([]);
  const [error, setError] = useState<string | null>(null);
  const [ready, setReady] = useState(false);
  const [toolId, setToolId] = useState("E-02");
  const [parameter, setParameter] = useState("vibration");
  const [summary, setSummary] = useState("");
  const [severity, setSeverity] = useState<Severity>("medium");
  const [pending, setPending] = useState(false);

  const reload = useCallback(() => {
    return Promise.all([api.listTickets(siteId), api.listIncidents(siteId), api.listTools(siteId)]).then(
      ([nextTickets, nextIncidents, nextTools]) => {
        const visibleTools = nextTools.filter((tool) => toolInPlant(siteId, tool.id, tool.siteId));
        setTickets(nextTickets.filter((ticket) => toolInPlant(siteId, ticket.toolId)));
        setIncidents(nextIncidents.filter((incident) => toolInPlant(siteId, incident.toolId)));
        setTools(visibleTools);
        setToolId((current) => (visibleTools.some((tool) => tool.id === current) ? current : (visibleTools[0]?.id ?? current)));
      },
    );
  }, [siteId]);

  useEffect(() => {
    let cancelled = false;
    reload()
      .catch((reason: unknown) => {
        if (!cancelled) setError(reason instanceof ApiProblem ? reason.title : "Tickets did not load.");
      })
      .finally(() => {
        if (!cancelled) setReady(true);
      });
    return () => {
      cancelled = true;
    };
  }, [reload]);

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
      if (view.escalated) navigate(pathForIncident(incident.id));
    } catch (reason: unknown) {
      setError(reason instanceof ApiProblem ? reason.title : "The report was not accepted.");
      setPending(false);
    }
  }

  const ordered = [...tickets].sort(byNewestTime((ticket) => ticket.openedAt));
  const selected = ticketId ? ordered.find((ticket) => ticket.id === ticketId) : undefined;
  const missing = Boolean(ticketId) && ready && !selected;
  const problem = visibleProblem(error);
  const rows = ordered.map((ticket) => {
    const incident = incidents.find((item) => item.id === ticket.incidentId);
    const stage = incident?.stage;
    const path = pathForTicket(ticket);
    return {
      id: ticket.id,
      status: incident ? (
        <Chip tone={incident.stage === "closed" ? "ok" : severityTone(incident.severity)}>{STAGE_LABEL[incident.stage]}</Chip>
      ) : (
        "—"
      ),
      toolName: toolPlainName(tools, ticket.toolId),
      toolId: ticket.toolId,
      time: formatStamp(ticket.openedAt),
      detail: (
        <>
          <Link to={path}>{ticket.title}</Link>
          <div className="mt-1 font-mono text-mps-micro">
            <Link to={path}>{ticket.number}</Link>
            {stage && stage !== "closed" ? <span> · {stage === "ticketOpened" ? "Start" : "Open"}</span> : null}
            {incident ? <span className="text-mps-muted"> · {severityWord(incident.severity)}</span> : null}
          </div>
        </>
      ),
    };
  });

  return (
    <div>
      <div className="mb-4">
        <h1 className="text-mps-display font-semibold">Tickets</h1>
        <p className="text-mps-secondary">
          <Explain text="Normalized intake: IoT SPC, operator, QMS NCR, supplier notice, and the fault injector. An open incident on the same tool and parameter is linked." />
        </p>
      </div>
      {problem ? <p className="mb-3 text-mps-alarm-text">{problem}</p> : null}
      {missing ? <HistoryNote text={MISSING_TICKET} /> : null}
      {selected ? (
        <section className={`${panel} mb-3`}>
          <h2 className="mb-2 text-mps-title font-semibold">{selected.number}</h2>
          <p>
            {toolPlainName(tools, selected.toolId)} · {formatStamp(selected.openedAt)}
          </p>
          <p className="mt-2">{selected.title}</p>
          <p className="mt-2">
            <Link to={pathForTicket(selected)}>Open the investigation</Link>
            {" · "}
            <Link to="/tickets">All tickets</Link>
          </p>
        </section>
      ) : null}
      {!ticketId || missing ? <RecordList label="Tickets" rows={rows} empty="No tickets yet." /> : null}
      <CaseBriefs
        incidentIds={incidents.filter((incident) => incident.stage !== "closed").map((incident) => incident.id)}
        intro="Evidence linked to these tickets"
      />
      <form className={`${panel} mt-3 grid gap-3 md:grid-cols-2`} onSubmit={onSubmit}>
        <h2 className="text-mps-title font-semibold md:col-span-2">Operator report</h2>
        <label className="text-mps-label text-mps-muted">
          Tool
          <select className={field} value={toolId} onChange={(event) => setToolId(event.target.value)}>
            {tools.map((tool) => (
              <option key={tool.id} value={tool.id}>
                {plainTool(tool.id) ?? tool.name} · {tool.model}
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
    </div>
  );
}
