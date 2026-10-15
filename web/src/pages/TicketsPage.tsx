// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company
import { useCallback, useEffect, useState, type FormEvent } from "react";
import { Link, useNavigate, useParams } from "react-router-dom";
import { api, ApiProblem } from "../api/client";
import { formatStamp, STAGE_LABEL, severityTone, severityWord } from "../api/labels";
import type { Incident, PrintoutConfirmation, PrintoutConfirmRequest, Severity, Ticket, Tool } from "../api/types";
import { CaseBriefs } from "../components/CaseBriefs";
import { Chip } from "../components/Chip";
import { Explain } from "../components/Explain";
import { HistoryNote, RecordList } from "../components/RecordList";
import { btnPrimary, field, panel } from "../components/ui";
import { useSetLifecycle } from "../context/ConsoleContext";
import type { FleetSensor } from "../fleet/sensors";
import { fleetSensors } from "virtual:fleet-sensors";
import { sensorLabel, slotWord } from "../fleet/schematic";
import { MISSING_TICKET, visibleProblem } from "../history/copy";
import { byNewestTime, toolPlainName } from "../history/order";
import { plainTool } from "../investigation/format";
import { useSite } from "../context/SiteContext";
import { pathForIncident, pathForTicket } from "../investigation/ticketRoute";
import { toolInPlant } from "../sites/selection";
import { lifecycleForIncident } from "../lifecycle";
import { parameterLabel } from "../sites/parameters";

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
  const [sensorId, setSensorId] = useState(() => sensorsFor("E-02")[0]?.id ?? "");
  const [summary, setSummary] = useState("");
  const [severity, setSeverity] = useState<Severity>("medium");
  const [pending, setPending] = useState(false);
  const [draft, setDraft] = useState<PrintoutConfirmRequest | null>(null);
  const [uploadNote, setUploadNote] = useState<string | null>(null);
  const [uploadError, setUploadError] = useState<string | null>(null);
  const [uploadPending, setUploadPending] = useState(false);
  const toolSensors = sensorsFor(toolId);
  const selected = toolSensors.find((sensor) => sensor.id === sensorId) ?? toolSensors[0];

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
      const incident = await api.openIncident({
        toolId,
        chamberId: selected?.chamberId,
        parameter: selected ? selected.parameter ?? selected.id : "",
        summary,
        severity,
      });
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
  const selectedTicket = ticketId ? ordered.find((ticket) => ticket.id === ticketId) : undefined;
  const missing = Boolean(ticketId) && ready && !selectedTicket;
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
      {selectedTicket ? (
        <section className={`${panel} mb-3`}>
          <h2 className="mb-2 text-mps-title font-semibold">{selectedTicket.number}</h2>
          <p>
            {toolPlainName(tools, selectedTicket.toolId)} · {formatStamp(selectedTicket.openedAt)}
          </p>
          <p className="mt-2">{selectedTicket.title}</p>
          <p className="mt-2">
            <Link to={pathForTicket(selectedTicket)}>Open the investigation</Link>
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
      <div className="mt-3 grid gap-3 xl:grid-cols-2">
        <form className={`${panel} grid min-w-0 gap-3`} onSubmit={onSubmit}>
          <h2 className="text-mps-title font-semibold">Operator report</h2>
          <label className="text-mps-label text-mps-muted">
            Tool
            <select className={field} value={toolId} onChange={(event) => chooseTool(event.target.value)}>
              {tools.map((tool) => (
                <option key={tool.id} value={tool.id}>
                  {plainTool(tool.id) ?? tool.name} · {tool.model}
                </option>
              ))}
            </select>
          </label>
          <label className="min-w-0 text-mps-label text-mps-muted">
            Parameter
            <select
              className={field}
              aria-label="Parameter"
              value={selected?.id ?? ""}
              onChange={(event) => setSensorId(event.target.value)}
              required
            >
              {toolSensors.map((sensor) => (
                <option key={sensor.id} value={sensor.id}>
                  {sensorOptionLabel(sensor)}
                </option>
              ))}
            </select>
            {selected ? (
              <span data-selected-sensor="" className="mt-1 block break-words font-mono text-mps-secondary">
                {sensorOptionLabel(selected)}
              </span>
            ) : null}
          </label>
          <label className="text-mps-label text-mps-muted">
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
          <div>
            <button className={btnPrimary} type="submit" disabled={pending || !selected}>
              {pending ? "Sending…" : "Open ticket"}
            </button>
          </div>
        </form>
        <section className={`${panel} grid min-w-0 gap-3`}>
          <h2 className="text-mps-title font-semibold">Upload telemetry</h2>
          <p className="break-words text-mps-secondary">
            <Explain text="Upload a PDF or a PNG from a bench instrument. Check the reading, then confirm it." />
          </p>
          <label className="text-mps-label text-mps-muted">
            Printout
            <input
              className={field}
              type="file"
              accept="application/pdf,image/png,image/jpeg,.pdf,.png,.jpg,.jpeg"
              onChange={(event) => {
                const file = event.target.files?.[0];
                if (file) void onUpload(file);
              }}
            />
          </label>
          {uploadError ? <p className="break-words text-mps-alarm-text">{uploadError}</p> : null}
          {draft ? (
            <div className="grid min-w-0 gap-3">
              <label className="text-mps-label text-mps-muted">
                Tool
                <input className={field} value={draft.toolId} onChange={(event) => patchDraft({ toolId: event.target.value })} />
              </label>
              <label className="text-mps-label text-mps-muted">
                Chamber
                <input className={field} value={draft.chamberId ?? ""} onChange={(event) => patchDraft({ chamberId: event.target.value })} />
              </label>
              <label className="text-mps-label text-mps-muted">
                Observed at
                <input className={field} value={draft.observedAt} onChange={(event) => patchDraft({ observedAt: event.target.value })} />
              </label>
              <label className="text-mps-label text-mps-muted">
                Instrument
                <select className={field} value={draft.instrument} onChange={(event) => patchDraft({ instrument: event.target.value })}>
                  {instrumentChoices(draft.instrument).map((choice) => (
                    <option key={choice.id} value={choice.id}>
                      {choice.label}
                    </option>
                  ))}
                </select>
              </label>
              <label className="text-mps-label text-mps-muted">
                Parameter
                <input className={field} aria-label="Printed parameter" value={draft.parameter} onChange={(event) => patchDraft({ parameter: event.target.value })} />
              </label>
              <label className="text-mps-label text-mps-muted">
                Value
                <input className={field} inputMode="decimal" value={String(draft.value)} onChange={(event) => patchNumber(event.target.value, "value")} />
              </label>
              <label className="text-mps-label text-mps-muted">
                Unit
                <input className={field} value={draft.unit} onChange={(event) => patchDraft({ unit: event.target.value })} />
              </label>
              <label className="text-mps-label text-mps-muted">
                Spec minimum
                <input className={field} inputMode="decimal" value={String(draft.specMinimum)} onChange={(event) => patchNumber(event.target.value, "specMinimum")} />
              </label>
              <label className="text-mps-label text-mps-muted">
                Spec maximum
                <input className={field} inputMode="decimal" value={String(draft.specMaximum)} onChange={(event) => patchNumber(event.target.value, "specMaximum")} />
              </label>
              <label className="text-mps-label text-mps-muted">
                Shape
                <select className={field} value={draft.shape ?? ""} onChange={(event) => patchDraft({ shape: event.target.value || null })}>
                  <option value="">none</option>
                  <option value="step">step</option>
                  <option value="ramp">ramp</option>
                  <option value="oscillation">oscillation</option>
                  <option value="intermittent">intermittent</option>
                </select>
              </label>
              <label className="text-mps-label text-mps-muted">
                Operator
                <input className={field} value={draft.operatorInitials ?? ""} onChange={(event) => patchDraft({ operatorInitials: event.target.value })} />
              </label>
              <div>
                <button className={btnPrimary} type="button" disabled={uploadPending} onClick={() => void onConfirm()}>
                  {uploadPending ? "Saving…" : "Confirm reading"}
                </button>
              </div>
            </div>
          ) : null}
          {uploadNote ? <p className="break-words text-mps-secondary">{uploadNote}</p> : null}
        </section>
      </div>
    </div>
  );

  function chooseTool(next: string) {
    const current = fleetSensors.find((sensor) => sensor.id === sensorId);
    const nextSensors = sensorsFor(next);
    const kept = nextSensors.find((sensor) => (sensor.parameter ?? sensor.id) === (current?.parameter ?? current?.id));
    setToolId(next);
    setSensorId((kept ?? nextSensors[0])?.id ?? "");
  }

  function patchDraft(patch: Partial<PrintoutConfirmRequest>) {
    setDraft((current) => (current ? { ...current, ...patch } : current));
  }

  function patchNumber(text: string, key: "value" | "specMinimum" | "specMaximum") {
    const value = Number(text);
    if (Number.isFinite(value)) patchDraft({ [key]: value });
  }

  async function onUpload(file: File) {
    setUploadPending(true);
    setUploadError(null);
    setUploadNote(null);
    try {
      const inspection = await api.inspectPrintout(file);
      setDraft({
        toolId: inspection.toolId,
        chamberId: inspection.chamberId,
        observedAt: inspection.observedAt,
        instrument: inspection.instrument,
        parameter: inspection.parameter,
        value: inspection.value,
        unit: inspection.unit,
        specMinimum: inspection.specMinimum,
        specMaximum: inspection.specMaximum,
        shape: inspection.shape,
        operatorInitials: inspection.operatorInitials,
      });
    } catch (reason: unknown) {
      setDraft(null);
      setUploadError(reason instanceof ApiProblem ? reason.title : "The printout could not be read.");
    } finally {
      setUploadPending(false);
    }
  }

  async function onConfirm() {
    if (!draft) return;
    setUploadPending(true);
    setUploadError(null);
    try {
      const confirmed = await api.confirmPrintout(draft);
      setUploadNote(confirmNote(draft, confirmed));
      await reload();
    } catch (reason: unknown) {
      setUploadError(reason instanceof ApiProblem ? reason.title : "The reading was not stored.");
    } finally {
      setUploadPending(false);
    }
  }
}

function sensorsFor(toolId: string): FleetSensor[] {
  return fleetSensors
    .filter((sensor) => sensor.toolId === toolId)
    .slice()
    .sort((left, right) => sensorOptionLabel(left).localeCompare(sensorOptionLabel(right)));
}

function sensorOptionLabel(sensor: FleetSensor): string {
  const named = sensor.parameter ? parameterLabel(sensor.parameter) : sensorLabel(sensor.schematicAnchor);
  const plain = !sensor.parameter || named === sensor.parameter ? sensorLabel(sensor.schematicAnchor) : named;
  return `${slotWord(sensor.schematicAnchor)} · ${plain} (${sensor.unit}), spec ${specNumber(sensor.limits.minimum)}-${specNumber(sensor.limits.maximum)}`;
}

function specNumber(value: number): string {
  return String(value);
}

function instrumentChoices(current: string): { id: string; label: string }[] {
  const choices = [
    { id: "oscilloscope", label: "Oscilloscope" },
    { id: "rfPowerMeter", label: "RF power meter" },
    { id: "particleCounter", label: "Particle counter" },
    { id: "thermocoupleLogger", label: "Thermocouple logger" },
  ];
  if (!choices.some((choice) => choice.id === current)) choices.push({ id: current, label: current });
  return choices;
}

function confirmNote(draft: PrintoutConfirmRequest, result: PrintoutConfirmation): string {
  if (!result.outOfSpec) return "The reading is inside the printed spec. It was stored as evidence.";
  const motion =
    draft.shape === "oscillation"
      ? "An oscillation"
      : draft.shape === "ramp"
        ? "A ramp"
        : draft.shape === "intermittent"
          ? "An intermittent change"
          : "A step";
  return `${motion} was applied to ${parameterLabel(result.catalogParameter)} on ${draft.toolId} and an alert was opened.`;
}
