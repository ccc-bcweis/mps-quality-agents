// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company
import { useConsole, useSetLifecycle } from "../context/ConsoleContext";
import { visibleProblem } from "../history/copy";
import { formatUsd } from "../api/labels";
import { Explain } from "../components/Explain";
import { Term } from "../components/Term";
import { panel } from "../components/ui";

function metricText(value: number | null | undefined, digits = 0, suffix = ""): string {
  if (value === null || value === undefined) return "—";
  return `${value.toFixed(digits)}${suffix}`;
}

export function MetricsPage() {
  useSetLifecycle("alert");
  const { metrics, metricsError, bannerMode } = useConsole();

  return (
    <div>
      <div className="mb-4">
        <h1 className="text-mps-display font-semibold">Metrics / eval</h1>
        <p className="max-w-3xl text-mps-secondary">
          <Explain text="Numbers below are the gateway payload from the metrics API. Empty fields stay empty until the eval runner reports them. The gates are the SPEC targets, not a score." />
        </p>
      </div>
      {visibleProblem(metricsError) ? <p className="mb-3 text-mps-alarm-text">{visibleProblem(metricsError)}</p> : null}
      <div className="grid grid-cols-2 gap-3 md:grid-cols-3 xl:grid-cols-6">
        <Stat label="Mode" value={bannerMode === "mockReasoner" ? "MOCK" : (metrics?.mode ?? "—").toUpperCase()} note="gateway" />
        <Stat label="Completion" value={metricText(metrics?.completionRate === null || metrics?.completionRate === undefined ? null : metrics.completionRate * 100, 0, "%")} note="task completion" />
        <Stat label="Grounding" value={metricText(metrics?.groundingScore === null || metrics?.groundingScore === undefined ? null : metrics.groundingScore * 100, 0, "%")} note="citation precision" />
        <Stat label="Category acc." value={metricText(metrics?.categoryAccuracy === null || metrics?.categoryAccuracy === undefined ? null : metrics.categoryAccuracy * 100, 0, "%")} note="top-1" />
        <Stat label="p95" value={metrics?.latencyP95Ms === null || metrics?.latencyP95Ms === undefined ? "—" : `${metrics.latencyP95Ms} ms`} note="investigation" />
        <Stat label="Cost / success" value={metrics?.costPerInvestigationUsd === null || metrics?.costPerInvestigationUsd === undefined ? "—" : formatUsd(metrics.costPerInvestigationUsd)} note="unit cost" />
      </div>
      <div className="mt-3 grid gap-3 lg:grid-cols-2">
        <section className={panel}>
          <h2 className="mb-2 text-mps-title font-semibold">
            Gates (<Explain text="SPEC" spellOut={false} /> §4.3)
          </h2>
          <table className="w-full text-left">
            <tbody>
              <tr className="border-t border-mps-line"><td className="py-1.5">Safe escalation</td><td className="py-1.5 font-mono">100% hard gate</td></tr>
              <tr className="border-t border-mps-line"><td className="py-1.5">Injection resistance</td><td className="py-1.5 font-mono">100% hard gate</td></tr>
              <tr className="border-t border-mps-line"><td className="py-1.5">Category accuracy</td><td className="py-1.5 font-mono">≥ 80%</td></tr>
              <tr className="border-t border-mps-line"><td className="py-1.5">Grounding</td><td className="py-1.5 font-mono">≥ 90%</td></tr>
              <tr className="border-t border-mps-line"><td className="py-1.5">Tool failures</td><td className="py-1.5 font-mono">{metrics?.toolFailureCount ?? "—"}</td></tr>
              <tr className="border-t border-mps-line"><td className="py-1.5">Spend today</td><td className="py-1.5 font-mono">{metrics ? `${formatUsd(metrics.spendTodayUsd)} of ${formatUsd(metrics.spendCapUsd)}` : "—"}</td></tr>
            </tbody>
          </table>
        </section>
        <section className={panel}>
          <h2 className="mb-2 text-mps-title font-semibold">Confusion sketch</h2>
          <p className="mb-2 text-mps-micro text-mps-warn">Illustrative layout only. This is not an eval report. E1 publishes the matrix.</p>
          <table className="w-full text-left font-mono text-mps-micro">
            <thead>
              <tr className="text-mps-muted">
                <th className="py-1 font-medium"> </th>
                <th className="py-1 font-medium">
                  <Term id="ECO" spellOut={false} />
                </th>
                <th className="py-1 font-medium">Cal</th>
                <th className="py-1 font-medium">Lot</th>
                <th className="py-1 font-medium">Drift</th>
              </tr>
            </thead>
            <tbody>
              <tr className="border-t border-mps-line">
                <td className="py-1">
                  <Term id="ECO" spellOut={false} />
                </td>
                <td>—</td>
                <td>—</td>
                <td>—</td>
                <td>—</td>
              </tr>
              <tr className="border-t border-mps-line"><td className="py-1">Cal</td><td>—</td><td>—</td><td>—</td><td>—</td></tr>
              <tr className="border-t border-mps-line"><td className="py-1">Lot</td><td>—</td><td>—</td><td>—</td><td>—</td></tr>
              <tr className="border-t border-mps-line"><td className="py-1">Drift</td><td>—</td><td>—</td><td>—</td><td>—</td></tr>
            </tbody>
          </table>
        </section>
      </div>
    </div>
  );
}

function Stat({ label, value, note }: { label: string; value: string; note: string }) {
  return (
    <div className={panel}>
      <div className="font-mono text-mps-micro uppercase text-mps-muted">{label}</div>
      <div className="font-mono text-mps-kpi">{value}</div>
      <div className="text-mps-micro text-mps-muted">{note}</div>
    </div>
  );
}
