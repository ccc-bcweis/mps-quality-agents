// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company
import { useEffect, useMemo, useRef, useState } from "react";
import { Link, useParams, useSearchParams } from "react-router-dom";
import { api, ApiProblem } from "../api/client";
import { FAMILY_PROCESS } from "../api/labels";
import type { Tool } from "../api/types";
import { SpcChart } from "../components/SpcChart";
import { Chip } from "../components/Chip";
import { Explain } from "../components/Explain";
import { PlainMetrics } from "../components/PlainMetrics";
import { ToolSchematic } from "../components/ToolSchematic";
import { btn, btnPrimary, panel } from "../components/ui";
import { useSetLifecycle } from "../context/ConsoleContext";
import { chartSpec } from "../fleet/chartSpec";
import { statusTone, storyFor, windowRows } from "../fleet/catalog";
import { sensorsForTool } from "../fleet/sensors";
import { liveReadingMap, overlayWindows, seriesPoints, storyWithLive } from "../live/overlay";
import { usePolled } from "../live/usePolled";
import { fleetSensors, fleetTemplates } from "virtual:fleet-sensors";
import { S1_INVESTIGATION_ID, S2_INVESTIGATION_ID } from "../mocks/demoData";

const INVESTIGATION_LINK: Record<string, string> = {
  "E-02": S1_INVESTIGATION_ID,
  "D-01": S2_INVESTIGATION_ID,
};

export function ToolPage() {
  const { toolId = "" } = useParams();
  const [searchParams] = useSearchParams();
  const streaming = searchParams.get("streaming") === "1";
  const focusParameter = searchParams.get("parameter");
  const [tool, setTool] = useState<Tool | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [selectedId, setSelectedId] = useState<string | null>(null);
  const focused = useRef(false);
  const live = usePolled(() => api.getToolLive(toolId), toolId);
  const story = tool ? storyFor(tool) : null;
  useSetLifecycle(story?.lifecycle ?? "alert", story?.escalated ?? false);
  const toolSensors = useMemo(
    () => (tool ? sensorsForTool(tool.id, fleetSensors, fleetTemplates) : []),
    [tool],
  );

  useEffect(() => {
    let cancelled = false;
    setTool(null);
    setError(null);
    setSelectedId(null);
    api
      .getTool(toolId)
      .then((next) => {
        if (!cancelled) setTool(next);
      })
      .catch((reason: unknown) => {
        if (!cancelled) setError(reason instanceof ApiProblem ? reason.title : "Tool was not found.");
      });
    return () => {
      cancelled = true;
    };
  }, [toolId]);

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

  const liveTile = live && live.readings.length > 0 ? live : undefined;
  const shown = storyWithLive(story, liveTile, liveTile ? liveTile.readings.slice(0, 12).map((reading) => reading.value) : undefined);
  const tone = statusTone(shown.status);
  const investigationId = INVESTIGATION_LINK[tool.id];
  const selected = toolSensors.find((sensor) => sensor.id === selectedId) ?? null;
  const chart = selected ? chartSpec(selected, story) : story.chart;
  const seriesParameter = selected?.parameter ?? focusParameter ?? chart.parameter;
  const livePoints = seriesPoints(live, seriesParameter, selected?.chamberId);
  const pointCount = livePoints && livePoints.length > 0 ? livePoints.length : 60;
  const readings = liveReadingMap(live?.readings);
  const windows = overlayWindows(windowRows(tool), live?.readings);

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
            <Link to="/">Fleet</Link> · {tool.bay} · {tool.model}
          </p>
          <h1 className="text-mps-display font-semibold">
            Tool {tool.id} · {FAMILY_PROCESS[tool.family]}
          </h1>
          <p className="max-w-3xl text-mps-secondary">
            {tool.chambers.map((chamber) => chamber.name).join(", ")} · recipe {story.recipe}. {story.note}
          </p>
        </div>
        <div className="flex gap-2">
          <Link className={btn} to="/tools/D-01">
            Open D-01 (S2 alarm)
          </Link>
          {investigationId ? (
            <Link className={btnPrimary} to={`/investigations/${investigationId}`}>
              Open investigation
            </Link>
          ) : null}
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
      <PlainMetrics toolIds={[tool.id]} />
      <div className={`${panel} mt-3`}>
        <ToolSchematic family={tool.family} sensors={toolSensors} selectedId={selectedId} onSelect={showSensor} readings={readings} />
      </div>
      <div id="spc-chart" className={`${panel} mt-3 scroll-mt-16`}>
        <h2 className="mb-2 text-mps-title font-semibold">
          <Explain text={`SPC · ${chart.label}`} />{" "}
          <span className="float-right font-mono text-mps-micro font-normal text-mps-muted">
            σ = {chart.sigma} {chart.unit} · n = {pointCount}
          </span>
        </h2>
        <SpcChart
          seedKey={`${tool.id}:${chart.parameter}`}
          label={chart.label}
          unit={chart.unit}
          mean={chart.mean}
          sigma={chart.sigma}
          shape={chart.shape}
          endValue={chart.endValue}
          points={livePoints}
        />
      </div>
      <div className="mt-3 grid gap-3 lg:grid-cols-2">
        <div className={panel}>
          <h2 className="mb-2 text-mps-title font-semibold">Operating windows (recipe {story.recipe})</h2>
          <table className="w-full text-left">
            <thead>
              <tr className="font-mono text-mps-micro uppercase text-mps-muted">
                <th className="py-1 font-medium">Parameter</th>
                <th className="py-1 font-medium">Window</th>
                <th className="py-1 font-medium">Now</th>
              </tr>
            </thead>
            <tbody>
              {windows.map((row) => (
                <tr key={row.parameter} className="border-t border-mps-line">
                  <td className="py-1.5">
                    <Explain text={row.label} />
                  </td>
                  <td className="py-1.5 font-mono">{row.window}</td>
                  <td className={`py-1.5 font-mono ${row.tone === "alarm" ? "text-mps-alarm-text" : row.tone === "warn" ? "text-mps-warn" : ""}`}>{row.now}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
        <div className={panel}>
          <h2 className="mb-2 text-mps-title font-semibold">Linked records</h2>
          {story.ticketNumber ? <p>Ticket <Link to="/tickets">{story.ticketNumber}</Link></p> : <p className="text-mps-muted">No open ticket on this tile.</p>}
          {investigationId ? (
            <p className="mt-2">
              Investigation <Link to={`/investigations/${investigationId}`}>{investigationId.slice(0, 8)}</Link>
            </p>
          ) : null}
          <p className="mt-3 font-mono text-mps-micro text-mps-muted">Deterministic rules draw the chart. The model has not written anything.</p>
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
