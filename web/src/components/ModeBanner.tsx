// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company
import { formatUsd } from "../api/labels";
import { useConsole } from "../context/ConsoleContext";
import { visibleProblem } from "../history/copy";
import { Term } from "./Term";

const ENGINE_TIP = "The model the server is using to answer. Set by Mps:Model:Mode (offline, openai or azure). It does not change the plant data.";

/** The AI engine the server really runs, from GET /api/model/status. Read-only: the mode is a server setting. */
export function ModeBanner() {
  const { metrics, metricsError, bannerMode, capReached, modelStatus } = useConsole();
  const spend = metrics ? formatUsd(bannerMode === "replay" ? 0 : metrics.spendTodayUsd) : "—";
  const cap = metrics ? formatUsd(metrics.spendCapUsd) : "—";
  const ratio = metrics && metrics.spendCapUsd > 0 && bannerMode !== "replay" ? metrics.spendTodayUsd / metrics.spendCapUsd : 0;
  const hot = ratio >= 0.8;
  const label = modelStatus ? modelStatus.label + (modelStatus.model ? ` · ${modelStatus.model}` : "") : "…";
  const warn = capReached || modelStatus?.fallingBack === true || modelStatus?.mode === "placeholder";
  const status = capReached
    ? "Daily spend cap reached. Live calls stay off and saved answers are used."
    : visibleProblem(metricsError)
      ? visibleProblem(metricsError)
      : modelStatus?.fallingBack || modelStatus?.mode === "placeholder"
        ? (modelStatus.detail ?? null)
        : null;

  return (
    <div className="flex flex-wrap items-center gap-x-3 gap-y-2 text-mps-body">
      <div role="group" aria-label="AI engine" title={ENGINE_TIP} className="flex flex-wrap items-center gap-2">
        <span className="shrink-0 whitespace-nowrap">
          <Term id="AI" spellOut={false} focusable={false} /> engine:
        </span>
        <span
          role="status"
          aria-label="AI engine in use"
          className={`shrink-0 whitespace-nowrap rounded border px-2 py-0.5 ${
            warn ? "border-mps-alarm text-sn-gold" : modelStatus?.live ? "border-mps-ok text-[#a7f3d0]" : "border-sn-gold bg-sn-navy-soft text-sn-gold"
          }`}
        >
          {label}
        </span>
      </div>
      <p className="shrink-0 whitespace-nowrap text-mps-micro text-sn-gold-wash">Answers only. Plant data stays the same.</p>
      {status ? <p className="basis-full text-sn-gold-wash">{status}</p> : null}
      <div className="ml-auto flex shrink-0 items-center gap-2 whitespace-nowrap" title="Hard daily spend cap enforced by the model gateway">
        <span className="text-mps-label text-mps-muted">Today's spend</span>
        <span className="h-1.5 w-24 overflow-hidden rounded-pill bg-mps-raised">
          <span className={`block h-full ${hot ? "bg-mps-alarm" : bannerMode === "live" ? "bg-mps-ok" : "bg-sn-gold"}`} style={{ width: `${Math.min(100, ratio * 100)}%` }} />
        </span>
        <span className="font-mono text-mps-micro text-mps-text">
          {spend} of {cap}
        </span>
      </div>
    </div>
  );
}
