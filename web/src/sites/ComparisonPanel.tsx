// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company
import type { FamilyComparison, ToolFamily } from "../api/types";
import { Explain } from "../components/Explain";
import { visibleProblem } from "../history/copy";
import { Term } from "../components/Term";
import { field } from "../components/ui";
import { lookupTerm } from "../glossary/terms";
import { formatPct } from "./floorLayout";
import { FAMILY_OPTIONS, parameterLabel, unitLabel } from "./parameters";

function formatReading(value: number): string {
  const abs = Math.abs(value);
  const digits = abs >= 20 ? 1 : abs >= 1 ? 2 : 3;
  return value.toLocaleString("en-US", { minimumFractionDigits: digits, maximumFractionDigits: digits });
}

function formatShare(value: number): string {
  return `${(value * 100).toLocaleString("en-US", { minimumFractionDigits: 1, maximumFractionDigits: 1 })}%`;
}

function Unit({ unit }: { unit: string }) {
  const label = unitLabel(unit);
  if (!lookupTerm(label)) return <span>{label}</span>;
  return <Term id={label} spellOut={false} />;
}

export function ComparisonPanel({
  comparison,
  family,
  error,
  onFamily,
}: {
  comparison: FamilyComparison | null;
  family: ToolFamily;
  error: string | null;
  onFamily: (family: ToolFamily) => void;
}) {
  const ordered = [...(comparison?.parameters ?? [])].sort((a, b) => {
    const aFlag = a.sites.some((site) => site.discrepancy) ? 0 : 1;
    const bFlag = b.sites.some((site) => site.discrepancy) ? 0 : 1;
    return aFlag - bFlag;
  });
  const flagged = ordered.filter((parameter) => parameter.sites.some((site) => site.discrepancy)).length;
  const referenceName =
    comparison?.parameters
      .flatMap((parameter) => parameter.sites)
      .find((site) => site.siteId === comparison.referenceSiteId)?.siteName ?? "the reference plant";

  return (
    <section aria-label="Cross-plant comparison" className="mt-6">
      <div className="mb-3 flex flex-wrap items-end justify-between gap-3">
        <div>
          <h2 className="text-mps-title font-semibold">Cross-plant comparison</h2>
          <p className="max-w-3xl text-mps-secondary">
            The same tool family at every plant. Average is the typical reading. Spread is how far the readings wander. Alarm rate is the share of readings outside the normal band. A row that differs is measured from {referenceName}.
          </p>
        </div>
        <label className="text-mps-body text-mps-muted" htmlFor="tool-family">
          Tool family
          <select
            id="tool-family"
            className={field}
            value={family}
            onChange={(event) => onFamily(event.target.value as ToolFamily)}
          >
            {FAMILY_OPTIONS.map((option) => (
              <option key={option.id} value={option.id}>
                {option.label}
              </option>
            ))}
          </select>
        </label>
      </div>
      {visibleProblem(error) ? <p className="text-mps-alarm-text">{visibleProblem(error)}</p> : null}
      {comparison ? (
        <>
          <p className="mb-3 text-mps-body">
            {flagged === 0
              ? `No parameter on this family sits apart from ${referenceName}.`
              : `${flagged} ${flagged === 1 ? "parameter differs" : "parameters differ"} from ${referenceName}. Parameters that differ are listed first.`}
          </p>
          <div className="space-y-2">
            {ordered.map((parameter) => (
              <article key={parameter.parameter} className="grid gap-2 rounded border border-mps-line bg-mps-panel p-3 lg:grid-cols-[minmax(160px,220px)_1fr_1fr_1fr]">
                <h3 className="text-mps-body font-semibold">
                  <Explain text={parameterLabel(parameter.parameter)} spellOut={false} />{" "}
                  <span className="font-normal text-mps-muted">
                    (<Unit unit={parameter.unit} />)
                  </span>
                </h3>
                {parameter.sites.map((site) => (
                  <div
                    key={site.siteId}
                    data-discrepancy={site.discrepancy ? "true" : "false"}
                    className={site.discrepancy ? "rounded border border-mps-warn bg-mps-raised p-2" : "rounded p-2"}
                  >
                    <p className="font-medium">{site.siteName}</p>
                    {site.siteId === comparison.referenceSiteId ? <p className="text-mps-micro text-mps-muted">Reference plant</p> : null}
                    {site.discrepancy ? <p className="text-mps-micro font-semibold text-mps-warn">Differs from the reference plant</p> : null}
                    <p className="mt-1 font-mono text-mps-micro text-mps-secondary">Average {formatReading(site.mean)}</p>
                    <p className="font-mono text-mps-micro text-mps-secondary">Spread {formatReading(site.sigma)}</p>
                    <p className="font-mono text-mps-micro text-mps-secondary">Alarm rate {formatShare(site.alarmRate)}</p>
                    <p className="font-mono text-mps-micro text-mps-secondary">
                      <Term id="Uptime" spellOut={false} /> {formatPct(site.uptimePct)}
                    </p>
                    <p className="font-mono text-mps-micro text-mps-secondary">
                      <Term id="Yield" spellOut={false} /> {formatPct(site.yieldPct)}
                    </p>
                    {site.discrepancy && site.reason ? <p className="mt-2 text-mps-body text-mps-secondary">{site.reason}</p> : null}
                  </div>
                ))}
              </article>
            ))}
          </div>
        </>
      ) : error ? null : (
        <p className="text-mps-muted">Loading the comparison…</p>
      )}
    </section>
  );
}
