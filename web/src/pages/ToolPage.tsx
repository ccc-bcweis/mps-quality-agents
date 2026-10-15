// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company
import { useEffect, useState } from "react";
import { Link, useParams } from "react-router-dom";
import { api, ApiProblem } from "../api/client";
import { FAMILY_PROCESS } from "../api/labels";
import type { Alert, Tool } from "../api/types";
import { SpcChart } from "../components/SpcChart";
import { Chip } from "../components/Chip";
import { Explain } from "../components/Explain";
import { PlainMetrics } from "../components/PlainMetrics";
import { ToolSchematic } from "../components/ToolSchematic";
import { btn, panel } from "../components/ui";
import { useSetLifecycle } from "../context/ConsoleContext";
import { visibleProblem } from "../history/copy";
import { useSite } from "../context/SiteContext";
import { toolInPlant } from "../sites/selection";
import { chartSpec } from "../fleet/chartSpec";
import { statusTone, storyFor, windowRows } from "../fleet/catalog";
import { fleetSensors } from "virtual:fleet-sensors";

export function ToolPage() {
  const { toolId = "" } = useParams();
  const { siteId } = useSite();
  const [tool, setTool] = useState<Tool | null>(null);
  const [alerts, setAlerts] = useState<Alert[]>([]);
  const [error, setError] = useState<string | null>(null);
  const [selectedId, setSelectedId] = useState<string | null>(null);
  const story = tool ? storyFor(tool, alerts) : null;
  useSetLifecycle(story?.lifecycle ?? "alert", story?.escalated ?? false);

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

  const tone = statusTone(story.status);
  const toolSensors = fleetSensors.filter((sensor) => sensor.toolId === tool.id);
  const selected = toolSensors.find((sensor) => sensor.id === selectedId) ?? null;
  const chart = selected ? chartSpec(selected, story) : story.chart;

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
            Open D-01
          </Link>
        </div>
      </div>
      <div className="grid grid-cols-2 gap-3 md:grid-cols-3 xl:grid-cols-6">
        <div className={panel}>
          <div className="font-mono text-mps-micro uppercase text-mps-muted">Status</div>
          <div className="mt-1">
            <Chip tone={tone}>{story.status === "watch" ? "WATCH" : story.status === "alarm" ? "ALARM" : "NOMINAL"}</Chip>
          </div>
        </div>
        {story.params.map((param) => (
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
        <ToolSchematic family={tool.family} sensors={toolSensors} selectedId={selectedId} onSelect={showSensor} />
      </div>
      <div id="spc-chart" className={`${panel} mt-3 scroll-mt-16`}>
        <h2 className="mb-2 text-mps-title font-semibold">
          <Explain text={`SPC · ${chart.label}`} />{" "}
          <span className="float-right font-mono text-mps-micro font-normal text-mps-muted">
            σ = {chart.sigma} {chart.unit} · n = 60
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
              {windowRows(tool).map((row) => (
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
