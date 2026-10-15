// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company
import { btnSm } from "../components/ui";
import { useEffect, useState } from "react";
import { Link } from "react-router-dom";
import { api, ApiProblem } from "../api/client";
import { formatStamp, INVESTIGATION_STAGE_LABEL } from "../api/labels";
import type { Incident, Investigation, Tool } from "../api/types";
import { RecordList } from "../components/RecordList";
import { useSite } from "../context/SiteContext";
import { visibleProblem } from "./copy";
import { byNewestTime, toolPlainName } from "./order";

export function InvestigationHistory({ detail }: { detail: "investigation" | "approval" }) {
  const [investigations, setInvestigations] = useState<Investigation[]>([]);
  const [incidents, setIncidents] = useState<Incident[]>([]);
  const [tools, setTools] = useState<Tool[]>([]);
  const [error, setError] = useState<string | null>(null);
  const [ready, setReady] = useState(false);
  const { siteId } = useSite();

  useEffect(() => {
    let cancelled = false;
    Promise.all([api.listInvestigations(siteId), api.listIncidents(siteId), api.listTools(siteId)])
      .then(([nextInvestigations, nextIncidents, nextTools]) => {
        if (cancelled) return;
        setInvestigations(nextInvestigations);
        setIncidents(nextIncidents);
        setTools(nextTools);
      })
      .catch((reason: unknown) => {
        if (!cancelled) setError(reason instanceof ApiProblem ? reason.title : "Investigations did not load.");
      })
      .finally(() => {
        if (!cancelled) setReady(true);
      });
    return () => {
      cancelled = true;
    };
  }, [siteId]);

  const problem = visibleProblem(error);
  if (!ready) return <p className="text-mps-muted">Loading investigations…</p>;
  if (problem) return <p className="text-mps-alarm-text">{problem}</p>;

  const incidentById = new Map(incidents.map((incident) => [incident.id, incident]));
  const rows = investigations
    .map((investigation) => {
      const incident = incidentById.get(investigation.incidentId);
      const toolId = incident?.toolId ?? investigation.evidence.find((item) => item.toolId)?.toolId ?? undefined;
      return {
        id: investigation.id,
        status: INVESTIGATION_STAGE_LABEL[investigation.stage],
        toolName: toolPlainName(tools, toolId),
        toolId,
        time: incident ? formatStamp(incident.openedAt) : "—",
        openedAt: incident?.openedAt ?? "",
        detail: (
          <>
            <div>{incident?.summary ?? "Investigation"}</div>
            <div className="mt-1">
              <Link to={detail === "approval" ? `/approvals/${investigation.id}` : `/investigations/${investigation.id}`} className={btnSm}>Open</Link>
            </div>
          </>
        ),
      };
    })
    .sort(byNewestTime((row) => row.openedAt));

  return (
    <RecordList
      label={detail === "approval" ? "Approvals" : "Investigations"}
      rows={rows}
      empty={detail === "approval" ? "No approvals yet." : "No investigations yet."}
    />
  );
}
