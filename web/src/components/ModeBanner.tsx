// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company
import { formatUsd } from "../api/labels";
import { useConsole } from "../context/ConsoleContext";

const COPY = {
  replay: {
    label: "REPLAY MODE",
    note: "Recorded responses. No live model calls.",
    action: "Switch to LIVE AI",
  },
  live: {
    label: "LIVE AI",
    note: "Live model calls. The gateway enforces the daily spend cap.",
    action: "Switch to REPLAY MODE",
  },
  mockReasoner: {
    label: "MOCK REASONER",
    note: "No replay recording for the last injection. A deterministic reasoner is in use.",
    action: "Switch to REPLAY MODE",
  },
} as const;

export function ModeBanner() {
  const { metrics, metricsError, bannerMode, serverMode, capReached, localPreview, setPreview } = useConsole();
  const copy = COPY[bannerMode];
  const spend = metrics ? formatUsd(bannerMode === "replay" ? 0 : metrics.spendTodayUsd) : "—";
  const cap = metrics ? formatUsd(metrics.spendCapUsd) : "—";
  const ratio = metrics && metrics.spendCapUsd > 0 && bannerMode !== "replay" ? metrics.spendTodayUsd / metrics.spendCapUsd : 0;
  const hot = ratio >= 0.8;

  const note = capReached
    ? "Daily spend cap reached. Live calls stay off and the console stays in replay."
    : metricsError
      ? metricsError
      : localPreview && serverMode
        ? `${copy.note} Local preview. Gateway reports ${serverMode}.`
        : copy.note;

  return (
    <div className={`sticky top-0 z-banner flex flex-wrap items-center gap-3 border-b px-4 py-1.5 text-mps-body ${bannerMode === "live" ? "border-mps-ok bg-[#06281d]" : "border-sn-gold bg-sn-navy"}`}>
      <span
        className={`rounded-pill px-2 py-0.5 font-mono text-mps-micro font-semibold tracking-wider ${
          bannerMode === "live"
            ? "bg-mps-ok-solid text-white"
            : bannerMode === "mockReasoner"
              ? "bg-mps-raised text-mps-ai"
              : "bg-sn-navy-soft text-sn-gold"
        }`}
      >
        {copy.label}
      </span>
      <p className={`min-w-[12rem] flex-1 ${bannerMode === "live" ? "text-[#a7f3d0]" : "text-sn-gold-wash"}`}>{note}</p>
      <button
        type="button"
        className={`rounded border px-2 py-0.5 ${bannerMode === "live" ? "border-mps-ok text-[#a7f3d0]" : "border-sn-gold text-sn-gold"} disabled:opacity-40`}
        disabled={capReached}
        onClick={() => setPreview(bannerMode === "replay" ? "live" : "replay")}
      >
        {capReached ? "Live locked" : copy.action}
      </button>
      <div className="ml-auto flex items-center gap-2" title="Hard daily spend cap enforced by the model gateway">
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
