// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company
import { useEffect, useMemo, useRef, useState } from "react";
import { Link, useParams, useSearchParams } from "react-router-dom";
import { api, ApiProblem } from "../api/client";
import { FAMILY_PROCESS, INVESTIGATION_STAGE_LABEL, formatStamp } from "../api/labels";
import type { Alert, Incident, Investigation, ManualReading, Ticket, Tool } from "../api/types";

/** Linked records shown on the tool page, newest first. */
const LINKED_LIMIT = 12;
import type { SchematicReading } from "../components/ToolSchematic";
import type { FleetSensor } from "../fleet/sensors";
import { linkedForTool, orderedLinked, type LinkedEntry } from "../fleet/linkedRecords";
import { useRecordEvents } from "../live/fleetHub";
import { AttachmentViewer } from "../components/AttachmentViewer";
import { ruleLabel } from "../api/labels";
import { SpcChart } from "../components/SpcChart";
import { Chip } from "../components/Chip";
import { Explain } from "../components/Explain";
import { PlainMetrics } from "../components/PlainMetrics";
import { ToolSchematic } from "../components/ToolSchematic";
import { btn, panel, btnSm } from "../components/ui";
import { useSetLifecycle } from "../context/ConsoleContext";
import { visibleProblem } from "../history/copy";
import { useSite } from "../context/SiteContext";
import { toolInPlant } from "../sites/selection";
import { chartSpec } from "../fleet/chartSpec";
import { statusTone, storyFor } from "../fleet/catalog";
import { sensorsForTool } from "../fleet/sensors";
import { liveReadingMap, overlayWindows, recipeLabel, seriesLimits, seriesPoints, storyWithLive } from "../live/overlay";
import { usePolled } from "../live/usePolled";
import { latestManual, manualMarker, manualTile, seriesFor } from "../live/manual";
import { statusReasons } from "../live/statusReasons";
import { fleetSensors, fleetTemplates } from "virtual:fleet-sensors";

export function ToolPage() {
  const { toolId = "" } = useParams();
  const [searchParams] = useSearchParams();
  const streaming = searchParams.get("streaming") === "1";
  const focusParameter = searchParams.get("parameter");
  const { siteId } = useSite();
  const [tool, setTool] = useState<Tool | null>(null);
  const [alerts, setAlerts] = useState<Alert[]>([]);
  const [records, setRecords] = useState<{ tickets: Ticket[]; incidents: Incident[]; investigations: Investigation[] }>({ tickets: [], incidents: [], investigations: [] });
  const [error, setError] = useState<string | null>(null);
  const [selectedId, setSelectedId] = useState<string | null>(null);
  const [viewing, setViewing] = useState<ManualReading | null>(null);
  const focused = useRef(false);
  const live = usePolled(() => api.getToolLive(toolId), toolId);
  const manualReadings = usePolled(() => api.listManualReadings(toolId), `manual:${toolId}`);
  const liveAlerts = usePolled(() => api.listAlerts(toolId, "all"), `alerts:${toolId}`);
  const story = tool ? storyFor(tool, alerts) : null;
  useSetLifecycle(story?.lifecycle ?? "alert", story?.escalated ?? false);
  const toolSensors = useMemo(
    () => (tool ? sensorsForTool(tool.id, fleetSensors, fleetTemplates) : []),
    [tool],
  );

  useEffect(() => {
    let cancelled = false;
    setTool(null);
    setAlerts([]);
    setError(null);
    setSelectedId(null);
    Promise.all([api.getTool(toolId), api.listAlerts(toolId, siteId).catch(() => [] as Alert[])])
      .then(([next, nextAlerts]) => {
        if (!cancelled) {
          setTool(next);
          setAlerts(nextAlerts);
        }
      })
      .catch((reason: unknown) => {
        if (!cancelled) {
          const title = reason instanceof ApiProblem ? reason.title : "Tool was not found.";
          setError(visibleProblem(title) ?? "Tool was not found.");
        }
      });
    return () => {
      cancelled = true;
    };
  }, [toolId, siteId]);

  // Alerts, tickets, incidents and investigations arrive over the fleet stream; reload the linked records when one changes.
  const [recordsVersion, setRecordsVersion] = useState(0);
  useRecordEvents(() => setRecordsVersion((version) => version + 1));

  useEffect(() => {
    let cancelled = false;
    Promise.all([
      api.listTickets(siteId).catch(() => [] as Ticket[]),
      api.listIncidents(siteId).catch(() => [] as Incident[]),
      api.listInvestigations(siteId).catch(() => [] as Investigation[]),
    ]).then(([tickets, incidents, investigations]) => {
      if (!cancelled) setRecords({ tickets, incidents, investigations });
    });
    return () => {
      cancelled = true;
    };
  }, [toolId, siteId, recordsVersion]);

  useEffect(() => {
    focused.current = false;
  }, [toolId, focusParameter]);

  useEffect(() => {
    if (!focusParameter || focused.current) return;
    const sensor =
      toolSensors.find((item) => item.parameter === focusParameter && item.chamberId?.endsWith("-A")) ??
      toolSensors.find((item) => item.parameter === focusParameter);
    if (!sensor) return;
    focused.current = true;
    setSelectedId(sensor.id);
  }, [focusParameter, toolSensors]);

  if (error) return <p className="text-mps-alarm-text">{error}</p>;
  if (!tool || !story) return <p className="text-mps-muted">Loading {toolId}…</p>;
  if (!toolInPlant(siteId, tool.id, tool.siteId)) {
    return (
      <div>
        <h1 className="text-mps-display font-semibold">Tool {tool.id}</h1>
        <p>This tool is at another plant. Choose All sites to open it.</p>
      </div>
    );
  }

  const liveTile = live && live.readings.length > 0 ? live : undefined;
  const shown = storyWithLive(story, liveTile, liveTile ? liveTile.readings.slice(0, 12).map((reading) => reading.value) : undefined);
  const tone = statusTone(shown.status);
  const selected = toolSensors.find((sensor) => sensor.id === selectedId) ?? null;
  const chart = selected ? chartSpec(selected, story) : story.chart;
  const seriesParameter = selected?.parameter ?? focusParameter ?? chart.parameter;
  const livePoints = seriesPoints(live, seriesParameter, selected?.chamberId);
  const serverLimits = seriesLimits(live, seriesParameter, selected?.chamberId);
  const pointCount = livePoints && livePoints.length > 0 ? livePoints.length : 60;
  const readings = withManualTiles(liveReadingMap(live?.readings), toolSensors, manualReadings);
  const chartManual = manualMarker(
    seriesFor(live, seriesParameter, selected?.chamberId),
    latestManual(manualReadings, seriesParameter, selected?.chamberId),
  );
  const reasons = statusReasons(shown.status, liveAlerts ?? alerts, manualReadings ?? []);
  const windows = overlayWindows(live?.readings);
  const recipe = recipeLabel(live, story?.recipe);
  const linked = linkedForTool(tool.id, { ...records, alerts: liveAlerts ?? alerts });
  const entries = orderedLinked(linked, records.incidents, manualReadings ?? [], LINKED_LIMIT);

  function showSensor(sensor: (typeof toolSensors)[number]) {
    setSelectedId(sensor.id);
    const node = document.getElementById("spc-chart");
    if (node && typeof node.scrollIntoView === "function") node.scrollIntoView({ block: "nearest" });
  }

  return (
    <div>
      <div className="mb-4 flex flex-wrap items-end justify-between gap-3">
        <div>
          <p className="font-mono text-mps-micro uppercase tracking-wider text-mps-muted">
            <Link className={btnSm} to="/">Production line</Link> · {tool.bay} · {tool.model}
          </p>
          <h1 className="text-mps-display font-semibold">
            Tool {tool.id} · {FAMILY_PROCESS[tool.family]}
          </h1>
          <p className="max-w-3xl text-mps-secondary">
            {tool.chambers.map((chamber) => chamber.name).join(", ")} · recipe {recipe}. {story.note}
          </p>
        </div>
      </div>
      {streaming ? (
        <p
          role="status"
          aria-label="Streaming trace"
          className="mb-3 max-w-full break-words rounded border border-mps-nominal bg-mps-panel px-3 py-2 text-mps-secondary"
        >
          <Explain text="This trace is streaming. An SPC rule opens the alert when the points cross the limit." />
        </p>
      ) : null}
      <div className="grid grid-cols-2 gap-3 md:grid-cols-3 xl:grid-cols-6">
        <div className={panel}>
          <div className="font-mono text-mps-micro uppercase text-mps-muted">Status</div>
          <div className="mt-1">
            <Chip tone={tone}>{shown.status === "watch" ? "WATCH" : shown.status === "alarm" ? "ALARM" : "NOMINAL"}</Chip>
          </div>
        </div>
        {shown.params.map((param) => (
          <div key={param.label} className={panel}>
            <div className="font-mono text-mps-micro uppercase text-mps-muted">
              <Explain text={param.label} spellOut={false} />
            </div>
            <div className={`font-mono text-mps-title ${param.tone === "alarm" ? "text-mps-alarm-text" : param.tone === "warn" ? "text-mps-warn" : ""}`}>
              {param.value}
            </div>
          </div>
        ))}
      </div>
      {reasons.length > 0 ? (
        <section aria-label="Why this status" className={`${panel} mt-3`}>
          <h2 className="mb-1 text-mps-title font-semibold">Why {tool.id} shows {shown.status === "alarm" ? "alarm" : "watch"}</h2>
          <ul className="grid gap-1">
            {reasons.map((reason) => (
              <li key={reason.alertId} className="flex flex-wrap items-center gap-2">
                <Link className={btnSm} to={`/alerts/${reason.alertId}`}>
                  Alert
                </Link>
                <span className="min-w-0 flex-1 break-words"><Explain text={reason.text} /></span>
              </li>
            ))}
          </ul>
        </section>
      ) : null}
      <PlainMetrics toolIds={[tool.id]} />
      <div className={`${panel} mt-3`}>
        <ToolSchematic family={tool.family} sensors={toolSensors} selectedId={selectedId} onSelect={showSensor} readings={readings} />
      </div>
      <div id="spc-chart" className={`${panel} mt-3 scroll-mt-16`}>
        <h2 className="mb-2 text-mps-title font-semibold">
          <Explain text={`${selected?.kind === "cumulative" ? "Count" : "SPC"} · ${chart.label}`} />{" "}
          <span className="float-right font-mono text-mps-micro font-normal text-mps-muted">
            σ = {chart.sigma} {chart.unit} · n = {pointCount}
          </span>
        </h2>
        <SpcChart
          seedKey={`${tool.id}:${chart.parameter}`}
          label={chart.label}
          unit={chart.unit}
          mean={serverLimits?.mean ?? chart.mean}
          sigma={serverLimits?.sigma ?? chart.sigma}
          shape={chart.shape}
          endValue={chart.endValue}
          points={livePoints}
          cumulative={selected?.kind === "cumulative"}
          manual={selected?.kind === "cumulative" ? undefined : chartManual}
        />
        {selected?.kind === "cumulative" ? (
          <p className="mt-2">
            {(() => {
              const rate = toolSensors.find((sensor) => sensor.type === "runRate");
              return rate ? (
                <button type="button" className={btnSm} onClick={() => showSensor(rate)}>
                  Show run rate (wafers/hour) with limits
                </button>
              ) : null;
            })()}
          </p>
        ) : null}
      </div>
      <div className="mt-3 grid gap-3 lg:grid-cols-2">
        <div className={panel}>
          <h2 className="mb-2 text-mps-title font-semibold">Operating windows (recipe {recipe})</h2>
          {windows.length === 0 ? (
            <p className="text-mps-muted">No live readings with a recipe window yet.</p>
          ) : (
            <table aria-label="Operating windows" className="w-full text-left">
              <thead>
                <tr className="font-mono text-mps-micro uppercase text-mps-muted">
                  <th className="py-1 font-medium">Parameter</th>
                  <th className="py-1 font-medium">
                    <Explain text="Recipe spec" spellOut={false} />
                  </th>
                  <th className="py-1 font-medium">
                    <Explain text="Control limits" spellOut={false} />
                  </th>
                  <th className="py-1 font-medium">Now</th>
                  <th className="py-1 font-medium">State</th>
                </tr>
              </thead>
              <tbody>
                {windows.map((row) => (
                  <tr key={row.parameter} className="border-t border-mps-line">
                    <td className="py-1.5">
                      <Explain text={row.label} />
                    </td>
                    <td className="py-1.5 font-mono">{row.window}</td>
                    <td className="py-1.5 font-mono">{row.control}</td>
                    <td className={`py-1.5 font-mono ${row.tone === "alarm" ? "text-mps-alarm-text" : row.tone === "warn" ? "text-mps-warn" : ""}`}>{row.now}</td>
                    <td className={`py-1.5 ${row.tone === "alarm" ? "text-mps-alarm-text" : row.tone === "warn" ? "text-mps-warn" : ""}`}>
                      <Explain text={row.state} spellOut={false} />
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          )}
        </div>
        <div className={panel}>
          <h2 className="mb-2 text-mps-title font-semibold">Linked records</h2>
          {entries.length === 0 ? (
            <p className="text-mps-muted">No tickets, alerts or investigations on this tool yet.</p>
          ) : (
            <ul aria-label="Linked records" className="grid gap-1.5">
              {entries.map((entry) => (
                <li key={`${entry.kind}-${entry.id}`} className="flex flex-wrap items-center gap-2" data-linked-kind={entry.kind} data-manual-reading={entry.kind === "manual" ? "true" : undefined}>
                  <LinkedEntryRow entry={entry} onOpen={setViewing} />
                </li>
              ))}
            </ul>
          )}
          {viewing && viewing.fileUrl ? (
            <AttachmentViewer url={viewing.fileUrl} fileName={viewing.fileName} title={viewing.fileName ?? "Printout"} onClose={() => setViewing(null)} />
          ) : null}
          <div className="mt-3 flex gap-2">
            <Link className={btn} to="/alerts">
              Alerts
            </Link>
            <Link className={btn} to="/tickets">
              Tickets
            </Link>
          </div>
        </div>
      </div>
    </div>
  );
}

/** Puts a confirmed bench reading on its sensor tile: the printed value, a Manual badge, colored by the printed spec. */
function withManualTiles(
  map: Map<string, SchematicReading> | undefined,
  sensors: FleetSensor[],
  manual: ManualReading[] | null,
): Map<string, SchematicReading> | undefined {
  if (!manual || manual.length === 0) return map;
  const next = new Map(map ?? []);
  for (const sensor of sensors) {
    if (!sensor.parameter) continue;
    const reading = latestManual(manual, sensor.parameter, sensor.chamberId);
    // Only a reading behind an active alert drives the tile; once resolved the live sensor value shows again.
    if (reading && reading.active && (!reading.chamberId || !sensor.chamberId || reading.chamberId === sensor.chamberId)) next.set(sensor.id, manualTile(reading));
  }
  return next;
}

function LinkedEntryRow({ entry, onOpen }: { entry: LinkedEntry; onOpen: (reading: ManualReading) => void }) {
  switch (entry.kind) {
    case "investigation":
      return (
        <>
          <Link className={btnSm} to={`/investigations/${entry.record.id}`}>
            Investigation
          </Link>
          <span className="min-w-0 flex-1 break-words">{entry.record.statusLabel ?? INVESTIGATION_STAGE_LABEL[entry.record.stage] ?? entry.record.stage}</span>
        </>
      );
    case "ticket":
      return (
        <>
          <Link className={btnSm} to={`/tickets/${encodeURIComponent(entry.record.id)}`}>
            {entry.record.number}
          </Link>
          <span className="min-w-0 flex-1 break-words">{entry.record.title}</span>
        </>
      );
    case "alert":
      return (
        <>
          <Link className={btnSm} to={`/alerts/${entry.record.id}`}>
            Alert
          </Link>
          <span className="min-w-0 flex-1 break-words">
            <Explain text={ruleLabel(entry.record.rule)} />
          </span>
        </>
      );
    case "manual": {
      const reading = entry.record;
      return (
        <>
          {reading.fileUrl ? (
            <button type="button" className={btnSm} onClick={() => onOpen(reading)}>
              Printout
            </button>
          ) : (
            <span className="font-mono text-mps-micro uppercase text-mps-muted">Manual</span>
          )}
          <span className={`min-w-0 flex-1 break-words ${reading.outOfSpec ? "text-mps-alarm-text" : ""}`}>
            {reading.source} Observed {formatStamp(reading.observedAt)}
            {reading.printedObservedAt ? ` (printed ${formatStamp(reading.printedObservedAt)}, set to now by the user)` : ""}.
            {reading.olderThanWindow ? " Older than the chart window." : ""}
          </span>
        </>
      );
    }
  }
}
