// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company
import { formatMillions, formatUsd } from "../api/labels";
import { Explain } from "../components/Explain";
import { panel } from "../components/ui";
import { useSetLifecycle } from "../context/ConsoleContext";
import { projectRoi, roiAssumptions } from "../roi/model";

export function RoiPage() {
  useSetLifecycle("alert");
  const roi = projectRoi();
  const assumptions = roiAssumptions;

  return (
    <div>
      <div className="mb-4">
        <h1 className="text-mps-display font-semibold">
          <Explain text="ROI" spellOut={false} /> · quality investigation acceleration
        </h1>
        <p className="text-mps-secondary">
          <Explain text="Illustrative. ROI here is a sketch from the plan, section 6.5, not a customer measurement. MTTR-Q is the investigation clock. COPQ is the cost of work that was not right the first time." />
        </p>
      </div>
      <p className="mb-3 rounded border border-mps-warn bg-mps-panel px-3 py-2">All figures are illustrative estimates for a 3-plant Meridian Precision Systems. Label every chart the same way.</p>
      <div className="grid grid-cols-2 gap-3 md:grid-cols-3 xl:grid-cols-6">
        <Stat label="Revenue" value={formatMillions(assumptions.revenueUsd)} />
        <Stat label="COPQ 8%" value={formatMillions(roi.copqUsd)} term="COPQ" />
        <Stat label="Incidents / yr" value={assumptions.incidentsPerYear.toLocaleString("en-US")} />
        <Stat label="MTTR-Q" value={`${assumptions.mttrDays} d`} term="MTTR-Q" />
        <Stat label="Time to hypothesis" value={`${assumptions.hypothesisDays.toFixed(1)} d`} />
        <Stat label="Repeat defect" value="18%" />
      </div>
      <div className="mt-3 grid gap-3 lg:grid-cols-2">
        <section className={panel}>
          <h2 className="mb-2 text-mps-title font-semibold">Baseline (estimated)</h2>
          <table className="w-full text-left">
            <tbody>
              <Row label="CAPA cycle time" value={`${assumptions.capaCycleDays} days`} term="CAPA" />
              <Row label="Engineer loaded cost" value={`${formatUsd(assumptions.engineerHourlyUsd, 0)} / h`} />
              <Row label="Hours / investigation" value={`${assumptions.hoursPerInvestigation} h`} />
              <Row label="Investigation labor" value={formatUsd(roi.laborUsd, 0)} />
              <Row label="Scrap / rework" value={formatUsd(roi.scrapUsd, 0)} />
              <Row label="Warranty / field" value={formatUsd(roi.warrantyUsd, 0)} />
              <Row label="Downtime" value={`${formatUsd(roi.downtimeUsd, 0)} · ${roi.downtimeHours.toFixed(0)} h at ${formatUsd(assumptions.downtimeHourlyUsd, 0)}/h`} />
            </tbody>
          </table>
          <p className="mt-2 text-mps-micro text-mps-muted">
            <Explain text="Labor is 1,400 × 22 h × $95. Scrap, warranty, and downtime split the rest of COPQ 50 / 20 / 30. That split is an assumption." spellOut={false} />
          </p>
        </section>
        <section className={panel}>
          <h2 className="mb-2 text-mps-title font-semibold">Projected levers</h2>
          <table className="w-full text-left">
            <thead>
              <tr className="font-mono text-mps-micro uppercase text-mps-muted">
                <th className="py-1 font-medium">Lever</th>
                <th className="py-1 font-medium">Conservative</th>
                <th className="py-1 font-medium">Expected</th>
              </tr>
            </thead>
            <tbody>
              <tr className="border-t border-mps-line"><td className="py-1.5">Investigation hours</td><td className="py-1.5 font-mono">−30%</td><td className="py-1.5 font-mono">−45%</td></tr>
              <tr className="border-t border-mps-line"><td className="py-1.5">Time to hypothesis</td><td className="py-1.5 font-mono">−50%</td><td className="py-1.5 font-mono">−70% → {roi.hypothesisDaysExpected.toFixed(1)} d</td></tr>
              <tr className="border-t border-mps-line"><td className="py-1.5">Repeat defects</td><td className="py-1.5 font-mono">−15%</td><td className="py-1.5 font-mono">−25%</td></tr>
              <tr className="border-t border-mps-line">
                <td className="py-1.5">
                  <Explain text="CAPA cycle" spellOut={false} />
                </td>
                <td className="py-1.5 font-mono">−20%</td>
                <td className="py-1.5 font-mono">−35% → {roi.capaDaysExpected.toFixed(1)} d</td>
              </tr>
              <tr className="border-t border-mps-line"><td className="py-1.5">Downtime hours avoided</td><td className="py-1.5 font-mono">−8%</td><td className="py-1.5 font-mono">−15%</td></tr>
            </tbody>
          </table>
          <p className="mt-3 font-mono text-mps-title">
            Savings {formatUsd(roi.conservativeSavingsUsd, 0)} – {formatUsd(roi.expectedSavingsUsd, 0)} / year
          </p>
          <p className="mt-2 text-mps-secondary">
            Dollar savings use labor, the repeat-defect slice of scrap, and downtime hours. Cycle time is shown in days so it is not counted twice. Model run cost at $0.06 × 1,400 is {formatUsd(roi.modelRunCostUsd, 2)}. Platform cost is not in the plan, so payback against model spend alone is not a customer figure.
          </p>
          <p className="mt-2 text-mps-micro text-mps-warn">The downtime-hours percent is an assumption. The plan, section 6.5, names the lever and does not set the percent.</p>
        </section>
      </div>
    </div>
  );
}

function Stat({ label, value, term }: { label: string; value: string; term?: string }) {
  const acronym = term ? label.slice(0, term.length) : "";
  const rest = term ? label.slice(term.length) : label;
  return (
    <div className={panel}>
      <div className="font-mono text-mps-micro uppercase text-mps-muted">
        {term ? (
          <>
            <Explain text={acronym} spellOut={false} />
            {rest}
          </>
        ) : (
          label
        )}
      </div>
      <div className="font-mono text-mps-title">{value}</div>
    </div>
  );
}

function Row({ label, value, term }: { label: string; value: string; term?: string }) {
  const acronym = term ? label.slice(0, term.length) : "";
  const rest = term ? label.slice(term.length) : label;
  return (
    <tr className="border-t border-mps-line">
      <td className="py-1.5">
        {term ? (
          <>
            <Explain text={acronym} spellOut={false} />
            {rest}
          </>
        ) : (
          label
        )}
      </td>
      <td className="py-1.5 font-mono">{value}</td>
    </tr>
  );
}
