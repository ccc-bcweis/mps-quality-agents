// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company
import { Term } from "./Term";
import { panel } from "./ui";
import {
  PERFORMANCE_CAPTION,
  formatHours,
  formatPercent,
  formatRate,
  performanceFor,
  rollup,
} from "../fleet/performance";

export function PlainMetrics({ toolIds, spellOut = true }: { toolIds: string[]; spellOut?: boolean }) {
  const rows = toolIds.flatMap((id) => {
    const row = performanceFor(id);
    return row ? [row] : [];
  });
  if (rows.length === 0) return null;
  const figures = rows.length === 1 ? rows[0] : rollup(rows);
  const single = rows.length === 1;
  return (
    <section className={`${panel} mt-3`} aria-label={single ? "Tool performance" : "Production line performance"}>
      <div className="mb-2 flex flex-wrap items-baseline justify-between gap-2">
        <h2 className="text-mps-title font-semibold">{single ? "This tool" : "Production line average"}</h2>
        <p className="text-mps-micro text-mps-muted">{PERFORMANCE_CAPTION}</p>
      </div>
      <dl className="grid grid-cols-2 gap-3 md:grid-cols-3 xl:grid-cols-6">
        <Metric term="Run rate" spellOut={spellOut} value={formatRate(figures.runRate)} />
        <Metric term="Uptime" spellOut={spellOut} value={formatPercent(figures.uptime)} />
        <Metric term="OEE" spellOut={spellOut} value={formatPercent(figures.oee)} />
        <Metric term="First-pass yield" spellOut={spellOut} value={formatPercent(figures.firstPassYield)} />
        <Metric term="MTBF" spellOut={spellOut} value={formatHours(figures.mtbfHours, 0)} />
        <Metric term="MTTR" spellOut={spellOut} value={formatHours(figures.mttrHours, 1)} />
      </dl>
    </section>
  );
}

export function TileMetrics({ toolId }: { toolId: string }) {
  const row = performanceFor(toolId);
  if (!row) return null;
  return (
    <p className="mt-2 flex flex-wrap gap-x-3 border-t border-mps-line pt-2 font-mono text-mps-micro text-mps-secondary">
      <span>
        <Term id="Run rate" spellOut={false} /> {formatRate(row.runRate)}
      </span>
      <span>
        <Term id="OEE" spellOut={false} /> {formatPercent(row.oee)}
      </span>
    </p>
  );
}

function Metric({ term, value, spellOut }: { term: string; value: string; spellOut: boolean }) {
  return (
    <div>
      <dt className="text-mps-micro text-mps-muted">
        <Term id={term} spellOut={spellOut} />
      </dt>
      <dd className="font-mono text-mps-title text-mps-text">{value}</dd>
    </div>
  );
}
