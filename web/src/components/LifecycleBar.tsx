// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company
import { LIFECYCLE, type LifecycleView } from "../lifecycle";

export function LifecycleBar({ current, escalated }: LifecycleView) {
  const currentIndex = LIFECYCLE.findIndex((item) => item.id === current);

  return (
    <ol aria-label="Incident lifecycle" className="m-0 flex list-none gap-0 overflow-x-auto border-b border-mps-line bg-mps-canvas px-4 py-2">
      {LIFECYCLE.map((item, index) => {
        const state = index < currentIndex ? "done" : index === currentIndex ? "current" : "todo";
        const color =
          state === "current" ? "text-mps-nominal" : state === "done" ? "text-mps-secondary" : "text-mps-muted";
        const label = item.id === "investigation" && escalated && state === "current" ? `${item.label} (escalated)` : item.label;
        return (
          <li
            key={item.id}
            aria-current={state === "current" ? "step" : undefined}
            className={`flex items-center font-mono text-mps-micro font-semibold uppercase tracking-wider ${color}`}
          >
            {index > 0 ? <span className="px-2 text-mps-line">›</span> : null}
            <span className={`mr-1 inline-block h-1.5 w-1.5 rounded-pill ${state === "todo" ? "bg-mps-line" : "bg-current"}`} />
            {label}
          </li>
        );
      })}
    </ol>
  );
}
