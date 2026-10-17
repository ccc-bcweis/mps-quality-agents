// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company
import { useEffect, useState } from "react";
import { Link } from "react-router-dom";
import { api, ApiProblem } from "../api/client";
import { FAMILY_PROCESS } from "../api/labels";
import type { Alert, Ticket, Tool } from "../api/types";
import { mpsColor } from "../brand/colors";
import { Chip } from "../components/Chip";
import { Sparkline } from "../components/Sparkline";
import { Explain } from "../components/Explain";
import { PlainMetrics, TileMetrics } from "../components/PlainMetrics";
import { btnPrimary, panel } from "../components/ui";
import { useSetLifecycle } from "../context/ConsoleContext";
import { visibleProblem } from "../history/copy";
import { useSite } from "../context/SiteContext";
import { bayCaption, statusTone, storyFor } from "../fleet/catalog";
import { storyWithLive } from "../live/overlay";
import { usePolled } from "../live/usePolled";
import { PLANTS, toolInPlant } from "../sites/selection";

export function FleetPage() {
  useSetLifecycle("alert");
  const { siteId } = useSite();
  const [tools, setTools] = useState<Tool[] | null>(null);
  const [alerts, setAlerts] = useState<Alert[]>([]);
  const [ticketRows, setTicketRows] = useState<Ticket[] | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [sparks, setSparks] = useState<Record<string, number[]>>({});
  const live = usePolled(() => api.listFleetLive(), "fleet");
  // Bay names come from data/fleet/sites.yaml through /api/sites, the same source as the floor plans.
  const [bayNames, setBayNames] = useState<Map<string, string>>(new Map());
  useEffect(() => {
    let cancelled = false;
    api
      .listSites()
      .then((sites) => {
        if (!cancelled) setBayNames(new Map(sites.flatMap((site) => site.bays.map((bay) => [bay.id, bay.name] as const))));
      })
      .catch(() => undefined);
    return () => {
      cancelled = true;
    };
  }, []);

  useEffect(() => {
    let cancelled = false;
    Promise.all([api.listTools(siteId), api.listTickets(siteId), api.listAlerts(undefined, siteId)])
      .then(([nextTools, nextTickets, nextAlerts]) => {
        if (cancelled) return;
        setTools(nextTools);
        setTicketRows(nextTickets);
        setAlerts(nextAlerts);
      })
      .catch((reason: unknown) => {
        if (!cancelled) setError(reason instanceof ApiProblem ? reason.title : "The fleet did not load.");
      });
    return () => {
      cancelled = true;
    };
  }, [siteId]);

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

  const rows = (tools ?? []).filter((tool) => toolInPlant(siteId, tool.id, tool.siteId));
  const visibleAlerts = alerts.filter((alert) => toolInPlant(siteId, alert.toolId));
  // Count only open tickets, from the ticket status the API derives from the incident.
  const tickets = ticketRows === null ? null : ticketRows.filter((ticket) => toolInPlant(siteId, ticket.toolId) && ticket.status !== "closed").length;
  const liveById = new Map((live?.tools ?? []).map((tool) => [tool.toolId, tool]));
  const stories = rows.map((tool) => ({ tool, story: storyWithLive(storyFor(tool, visibleAlerts), liveById.get(tool.id), sparks[tool.id]) }));
  const plantName = PLANTS.find((plant) => plant.id === siteId)?.name ?? "All sites";
  const bays = [...new Set(rows.map((tool) => tool.bay))];
  const families = new Set(rows.map((tool) => tool.family)).size;
  // Same rows the Alerts page lists for this plant, so the button and the page always agree.
  const openAlerts = visibleAlerts.filter((alert) => alert.status !== "cleared").length;

  return (
    <div>
      <div className="mb-4 flex flex-wrap items-end justify-between gap-3">
        <div>
          <p className="font-mono text-mps-micro uppercase tracking-wider text-mps-muted">
            {plantName} · {rows.length} tools · {families} families
          </p>
          <h1 className="text-mps-display font-semibold">Production line overview</h1>
          <p className="text-mps-secondary">
            <Explain text={`${rows.length} tools, ${families} families. SPC windows and Western Electric rules watch chamber pressure, ESC temperature, and RF power. Tiles open tool detail.`} />
          </p>
        </div>
        <div className="flex gap-2">
          <Link className={btnPrimary} to="/alerts">
            Open alerts ({openAlerts})
          </Link>
        </div>
      </div>
      {visibleProblem(error) ? <p className="mb-3 text-mps-alarm-text">{visibleProblem(error)}</p> : null}
      <div className="grid grid-cols-2 gap-3 md:grid-cols-3 xl:grid-cols-6">
        <Kpi label="Tools" value={tools ? String(rows.length) : "—"} note="fleet" />
        <Kpi label="Nominal" value={String(stories.filter((item) => item.story.status === "nominal").length)} note="in window" />
        <Kpi label="Watch" value={String(stories.filter((item) => item.story.status === "watch").length)} note="inside window, rule tripped" tone="warn" />
        <Kpi label="Alarm" value={String(stories.filter((item) => item.story.status === "alarm").length)} note="outside window" tone="alarm" />
        <Kpi label="Open tickets" value={tickets === null ? "—" : String(tickets)} note="queue" />
        <Kpi label="p95 time" value="—" note="investigation, from metrics" />
      </div>
      {tools && rows.length === 0 ? <p className="mt-4 text-mps-muted">No tools at this plant.</p> : null}
      {rows.length > 0 ? <PlainMetrics toolIds={rows.map((tool) => tool.id)} /> : null}
      {bays.map((bay) => (
        <section key={bay} className="mt-5">
          <h2 className="mb-2 font-mono text-mps-micro tracking-wider text-mps-muted">
            {bayNames.get(bay) ?? bay} · {bayCaption(rows.filter((tool) => tool.bay === bay).map((tool) => tool.family))}
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
                            <span className="min-w-0 break-words text-left text-mps-muted">
                              <Explain text={param.label} />
                            </span>
                            <span className={"shrink-0 text-right " + param.tone === "alarm" ? "text-mps-alarm-text" : param.tone === "warn" ? "text-mps-warn" : "text-mps-secondary"}>
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
