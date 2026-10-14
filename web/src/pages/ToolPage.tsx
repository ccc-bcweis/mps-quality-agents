// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company
import { useEffect, useState } from "react";
import { Link, useParams } from "react-router-dom";
import { api, ApiProblem } from "../api/client";
import { FAMILY_PROCESS } from "../api/labels";
import type { Tool } from "../api/types";
import { SpcChart } from "../components/SpcChart";
import { Chip } from "../components/Chip";
import { btn, btnPrimary, panel } from "../components/ui";
import { useSetLifecycle } from "../context/ConsoleContext";
import { statusTone, storyFor, windowRows } from "../fleet/catalog";
import { S1_INVESTIGATION_ID, S2_INVESTIGATION_ID } from "../mocks/demoData";

const INVESTIGATION_LINK: Record<string, string> = {
  "E-02": S1_INVESTIGATION_ID,
  "D-01": S2_INVESTIGATION_ID,
};

export function ToolPage() {
  const { toolId = "" } = useParams();
  const [tool, setTool] = useState<Tool | null>(null);
  const [error, setError] = useState<string | null>(null);
  const story = tool ? storyFor(tool) : null;
  useSetLifecycle(story?.lifecycle ?? "alert", story?.escalated ?? false);

  useEffect(() => {
    let cancelled = false;
    setTool(null);
    setError(null);
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

  if (error) return <p className="text-mps-alarm-text">{error}</p>;
  if (!tool || !story) return <p className="text-mps-muted">Loading {toolId}…</p>;

  const tone = statusTone(story.status);
  const investigationId = INVESTIGATION_LINK[tool.id];

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
      <div className="grid grid-cols-2 gap-3 md:grid-cols-3 xl:grid-cols-6">
        <div className={panel}>
          <div className="font-mono text-mps-micro uppercase text-mps-muted">Status</div>
          <div className="mt-1">
            <Chip tone={tone}>{story.status === "watch" ? "WATCH" : story.status === "alarm" ? "ALARM" : "NOMINAL"}</Chip>
          </div>
        </div>
        {story.params.map((param) => (
          <div key={param.label} className={panel}>
            <div className="font-mono text-mps-micro uppercase text-mps-muted">{param.label}</div>
            <div className={`font-mono text-mps-title ${param.tone === "alarm" ? "text-mps-alarm-text" : param.tone === "warn" ? "text-mps-warn" : ""}`}>
              {param.value}
            </div>
          </div>
        ))}
      </div>
      <div className={`${panel} mt-3`}>
        <h2 className="mb-2 text-mps-title font-semibold">
          SPC · {story.chart.label}{" "}
          <span className="float-right font-mono text-mps-micro font-normal text-mps-muted">
            σ = {story.chart.sigma} {story.chart.unit} · n = 60
          </span>
        </h2>
        <SpcChart
          seedKey={`${tool.id}:${story.chart.parameter}`}
          label={story.chart.label}
          unit={story.chart.unit}
          mean={story.chart.mean}
          sigma={story.chart.sigma}
          shape={story.chart.shape}
          endValue={story.chart.endValue}
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
                  <td className="py-1.5">{row.label}</td>
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
