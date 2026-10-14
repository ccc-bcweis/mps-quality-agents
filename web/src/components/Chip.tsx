// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company
import type { Tone } from "../api/labels";

const toneClass: Record<Tone, string> = {
  nominal: "bg-mps-nominal text-mps-canvas",
  warn: "bg-mps-warn text-mps-canvas",
  alarm: "bg-mps-alarm text-mps-canvas",
  ok: "bg-mps-ok text-mps-canvas",
  ai: "bg-mps-ai text-mps-canvas",
};

export function Chip({ tone, children }: { tone: Tone; children: string }) {
  return (
    <span className={`inline-flex items-center rounded-pill px-2 py-0.5 font-mono text-mps-micro font-semibold ${toneClass[tone]}`}>
      {children}
    </span>
  );
}

const ledClass: Record<Tone | "off", string> = {
  nominal: "bg-mps-nominal shadow-[0_0_6px_#22D3EE]",
  warn: "bg-mps-warn shadow-[0_0_6px_#F59E0B]",
  alarm: "bg-mps-alarm shadow-[0_0_6px_#EF4444]",
  ok: "bg-mps-ok shadow-[0_0_6px_#10B981]",
  ai: "bg-mps-ai shadow-[0_0_6px_#A78BFA]",
  off: "bg-mps-line",
};

export function Led({ tone }: { tone: Tone | "off" }) {
  return <span aria-hidden="true" className={`inline-block h-2 w-2 rounded-pill ${ledClass[tone]}`} />;
}
