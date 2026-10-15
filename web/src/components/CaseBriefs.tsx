// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company
import { useEffect, useState } from "react";
import { api } from "../api/client";
import type { Investigation } from "../api/types";
import { panel } from "./ui";
import { AgentTimeline } from "./AgentTimeline";
import { EvidencePack } from "./EvidencePack";

/** Evidence and the agent trace for incidents already on a list. The list itself stays put. */
export function CaseBriefs({ incidentIds, intro }: { incidentIds: string[]; intro: string }) {
  const idKey = Array.from(new Set(incidentIds.filter((id) => id.length > 0))).sort().join("|");
  const [packs, setPacks] = useState<Investigation[]>([]);

  useEffect(() => {
    const ids = idKey.length === 0 ? [] : idKey.split("|");
    let cancelled = false;
    Promise.all(
      ids.map(async (id) => {
        try {
          return await api.getLatestInvestigation(id);
        } catch {
          return null;
        }
      }),
    ).then((rows) => {
      if (!cancelled) setPacks(rows.filter((row): row is Investigation => row !== null));
    });
    return () => {
      cancelled = true;
    };
  }, [idKey]);

  if (packs.length === 0) return null;

  return (
    <div className="mt-3 space-y-3">
      <h2 className="text-mps-title font-semibold">{intro}</h2>
      {packs.map((investigation) => (
        <section key={investigation.id} className={panel}>
          <EvidencePack items={investigation.evidence} />
          <div className="mt-4">
            <AgentTimeline investigation={investigation} />
          </div>
        </section>
      ))}
    </div>
  );
}
