// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company
import { CATEGORY_LABEL } from "../labels";
import type { EvidenceStance, Investigation, RootCauseCategory } from "../api/types";
import { Explain } from "./Explain";

const CATEGORIES = Object.keys(CATEGORY_LABEL) as RootCauseCategory[];

export type MatrixRow = {
  evidenceId: string;
  label: string;
  cells: Partial<Record<RootCauseCategory, { stance: EvidenceStance; whyItMatters?: string | null }>>;
};

/** One row per evidence item the server's matrix rates, in evidence-pack order. Nothing here is authored on the client. */
export function matrixRows(investigation: Pick<Investigation, "evidence" | "evidenceMatrix">): MatrixRow[] {
  const entries = investigation.evidenceMatrix ?? [];
  if (entries.length === 0) return [];
  const titles = new Map(investigation.evidence.map((item) => [item.id, item.title]));
  const order = new Map(investigation.evidence.map((item, index) => [item.id, index]));
  const rows = new Map<string, MatrixRow>();
  for (const entry of entries) {
    const row = rows.get(entry.evidenceId) ?? { evidenceId: entry.evidenceId, label: titles.get(entry.evidenceId) ?? "Evidence", cells: {} };
    row.cells[entry.category] = { stance: entry.stance, whyItMatters: entry.whyItMatters };
    rows.set(entry.evidenceId, row);
  }
  return [...rows.values()].sort((left, right) => (order.get(left.evidenceId) ?? Number.MAX_SAFE_INTEGER) - (order.get(right.evidenceId) ?? Number.MAX_SAFE_INTEGER));
}

export function markLabel(stance: EvidenceStance | undefined): string {
  if (stance === "supports") return "supports";
  if (stance === "rulesOut") return "rules out";
  return "—";
}

export function EvidenceMatrix({ investigation }: { investigation: Pick<Investigation, "evidence" | "evidenceMatrix"> }) {
  const rows = matrixRows(investigation);
  if (rows.length === 0) return null;
  return (
    <div>
      <h2 className="mb-2 text-mps-title font-semibold">Root-cause evidence matrix</h2>
      <p className="mb-2 text-mps-secondary">Each row says whether that evidence supports a class or rules it out. An em dash is still open.</p>
      <div className="overflow-x-auto">
        <table aria-label="Root-cause evidence matrix" className="w-full text-left">
          <thead>
            <tr className="font-mono text-mps-micro uppercase text-mps-muted">
              <th className="py-1 pr-2 font-medium">Evidence</th>
              {CATEGORIES.map((category) => (
                <th key={category} className="py-1 pr-2 font-medium">
                  {CATEGORY_LABEL[category]}
                </th>
              ))}
            </tr>
          </thead>
          <tbody>
            {rows.map((row) => (
              <tr key={row.evidenceId} className="border-t border-mps-line">
                <th scope="row" className="py-1.5 pr-2 font-medium">
                  <Explain text={row.label} />
                </th>
                {CATEGORIES.map((category) => (
                  <td key={category} className="py-1.5 pr-2 font-mono text-mps-micro" title={row.cells[category]?.whyItMatters ?? undefined}>
                    {markLabel(row.cells[category]?.stance)}
                  </td>
                ))}
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  );
}
