// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company
import { useEffect, useState } from "react";
import { Link } from "react-router-dom";
import { api, ApiProblem } from "../api/client";
import { FAMILY_PROCESS } from "../api/labels";
import type { Tool } from "../api/types";
import { mpsColor } from "../brand/colors";
import { Chip } from "../components/Chip";
import { Sparkline } from "../components/Sparkline";
import { Explain } from "../components/Explain";
import { PlainMetrics, TileMetrics } from "../components/PlainMetrics";
import { btnPrimary, panel } from "../components/ui";
import { useSetLifecycle } from "../context/ConsoleContext";
import { bayCaption, statusTone, storyFor } from "../fleet/catalog";
import { storyWithLive } from "../live/overlay";
import { usePolled } from "../live/usePolled";

export function FleetPage() {
  useSetLifecycle("alert");
  const [tools, setTools] = useState<Tool[] | null>(null);
  const [tickets, setTickets] = useState<number | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [sparks, setSparks] = useState<Record<string, number[]>>({});
  const live = usePolled(() => api.listFleetLive(), "fleet");

  useEffect(() => {
    let cancelled = false;
    Promise.all([api.listTools(), api.listTickets()])
      .then(([nextTools, nextTickets]) => {
        if (cancelled) return;
        setTools(nextTools);
        setTickets(nextTickets.length);
      })
      .catch((reason: unknown) => {
        if (!cancelled) setError(reason instanceof ApiProblem ? reason.title : "The fleet did not load.");
      });
    return () => {
      cancelled = true;
    };
  }, []);

  useEffect(() => {
    if (!live || live.tools.length === 0) {
      setSparks((current) => (Object.keys(current).length === 0 ? current : {}));
      return;
    }
    setSparks((current) => {
      let changed = false;
      const next = { ...current };
      for (const tool of live.tools) {
        const headline = tool.readings[0];
        if (!headline) continue;
        const prior = next[tool.toolId] ?? [];
        if (prior.length > 0 && prior[prior.length - 1] === headline.value) continue;
        next[tool.toolId] = [...prior, headline.value].slice(-12);
        changed = true;
      }
      return changed ? next : current;
    });
  }, [live]);

  const rows = tools ?? [];
  const liveById = new Map((live?.tools ?? []).map((tool) => [tool.toolId, tool]));
  const stories = rows.map((tool) => ({ tool, story: storyWithLive(storyFor(tool), liveById.get(tool.id), sparks[tool.id]) }));
  const bays = [...new Set(rows.map((tool) => tool.bay))];

  return (
    <div>
      <div className="mb-4 flex flex-wrap items-end justify-between gap-3">
        <div>
          <p className="font-mono text-mps-micro uppercase tracking-wider text-mps-muted">Northwater teaching floor · eight tools</p>
          <h1 className="text-mps-display font-semibold">Fleet overview</h1>
          <p className="text-mps-secondary">
            <Explain text="Eight tools, four families. SPC windows and Western Electric rules watch chamber pressure, ESC temperature, and RF power. Tiles open tool detail." />
          </p>
        </div>
        <div className="flex gap-2">
          <Link className={btnPrimary} to="/alerts">
            Open alerts ({stories.filter((item) => item.story.status !== "nominal").length})
          </Link>
        </div>
      </div>
      {error ? <p className="mb-3 text-mps-alarm-text">{error}</p> : null}
      <div className="grid grid-cols-2 gap-3 md:grid-cols-3 xl:grid-cols-6">
        <Kpi label="Tools" value={tools ? String(tools.length) : "—"} note="fleet" />
        <Kpi label="Nominal" value={String(stories.filter((item) => item.story.status === "nominal").length)} note="in window" />
        <Kpi label="Watch" value={String(stories.filter((item) => item.story.status === "watch").length)} note="inside window, rule tripped" tone="warn" />
        <Kpi label="Alarm" value={String(stories.filter((item) => item.story.status === "alarm").length)} note="outside window" tone="alarm" />
        <Kpi label="Open tickets" value={tickets === null ? "—" : String(tickets)} note="queue" />
        <Kpi label="p95 time" value="—" note="investigation, from metrics" />
      </div>
      {rows.length > 0 ? <PlainMetrics toolIds={rows.map((tool) => tool.id)} /> : null}
      {bays.map((bay) => (
        <section key={bay} className="mt-5">
          <h2 className="mb-2 font-mono text-mps-micro uppercase tracking-widest text-mps-muted">
            {bay} · {bayCaption(bay)}
          </h2>
          <div className="grid grid-cols-1 gap-3 sm:grid-cols-2 xl:grid-cols-4">
            {stories
              .filter((item) => item.tool.bay === bay)
              .map(({ tool, story }) => {
                const tone = statusTone(story.status);
                const border = story.status === "alarm" ? "border-mps-alarm" : story.status === "watch" ? "border-mps-warn" : "border-mps-line";
                const spark = tone === "alarm" ? mpsColor.alarm : tone === "warn" ? mpsColor.warn : tone === "ok" ? mpsColor.ok : mpsColor.nominal;
                return (
                  <article key={tool.id} className={`${panel} ${border}`}>
                    <Link to={`/tools/${tool.id}`} className="block no-underline hover:bg-mps-raised">
                      <div className="flex items-center justify-between">
                        <span className="font-mono text-mps-title font-semibold">{tool.id}</span>
                        <Chip tone={tone}>{story.status === "watch" ? "WATCH" : story.status === "alarm" ? "ALARM" : "NOMINAL"}</Chip>
                      </div>
                      <p className="mt-1 text-mps-micro text-mps-muted">
                        {tool.model} · {FAMILY_PROCESS[tool.family]}
                      </p>
                      <div className="mt-2 space-y-1 font-mono text-mps-micro">
                        {story.params.map((param) => (
                          <div key={param.label} className="flex min-w-0 justify-between gap-2">
                            <span className="min-w-0 break-words text-mps-muted">
                              <Explain text={param.label} />
                            </span>
                            <span className={param.tone === "alarm" ? "text-mps-alarm-text" : param.tone === "warn" ? "text-mps-warn" : "text-mps-secondary"}>
                              {param.value}
                            </span>
                          </div>
                        ))}
                      </div>
                      <Sparkline values={story.spark} color={spark} />
                    </Link>
                    <TileMetrics toolId={tool.id} />
                  </article>
                );
              })}
          </div>
        </section>
      ))}
    </div>
  );
}

function Kpi({ label, value, note, tone }: { label: string; value: string; note: string; tone?: "warn" | "alarm" }) {
  const color = tone === "alarm" ? "text-mps-alarm" : tone === "warn" ? "text-mps-warn" : "text-mps-text";
  return (
    <div className={panel}>
      <div className="font-mono text-mps-micro uppercase tracking-wider text-mps-muted">{label}</div>
      <div className={`font-mono text-mps-kpi ${color}`}>{value}</div>
      <div className="text-mps-micro text-mps-muted">{note}</div>
    </div>
  );
}
