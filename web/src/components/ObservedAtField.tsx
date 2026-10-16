// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company
import { useEffect, useState } from "react";
import { api } from "../api/client";
import { formatStamp } from "../api/labels";
import type { ToolLive } from "../api/types";
import { isOlderThanWindow, windowStart } from "../live/manual";
import { btnPrimary, field } from "./ui";

/**
 * Observed time for a printout, with a Now button. Portable instruments often have the wrong clock, and old sheets
 * should read as fresh. Now sets the plant clock time and keeps the printed time for the audit trail.
 */
export function ObservedAtField({
  toolId,
  chamberId,
  parameter,
  value,
  printed,
  onChange,
  className = field,
  labelClassName = "text-mps-label text-mps-muted",
}: {
  toolId: string;
  chamberId?: string | null;
  parameter: string;
  value: string;
  printed?: string | null;
  onChange: (observedAt: string, printedObservedAt: string | null) => void;
  className?: string;
  labelClassName?: string;
}) {
  const [live, setLive] = useState<ToolLive | null>(null);
  useEffect(() => {
    let cancelled = false;
    if (!toolId) return;
    api
      .getToolLive(toolId)
      .then((next) => {
        if (!cancelled) setLive(next);
      })
      .catch(() => {
        if (!cancelled) setLive(null);
      });
    return () => {
      cancelled = true;
    };
  }, [toolId]);

  const start = windowStart(live, parameter, chamberId) ?? live?.series.find((series) => series.points.length > 0)?.points[0]?.at;
  const old = isOlderThanWindow(value, start);

  async function setNow() {
    let now = live?.simulatedNow;
    try {
      const fresh = await api.getToolLive(toolId);
      setLive(fresh);
      now = fresh.simulatedNow;
    } catch {
      // Keep the last clock we saw.
    }
    onChange(now ?? new Date().toISOString(), printed ?? value);
  }

  return (
    <div className="min-w-0">
      <label className={labelClassName}>
        Observed at
        <span className="flex min-w-0 items-center gap-2">
          <input className={className} aria-label="Observed at" value={value} onChange={(event) => onChange(event.target.value, printed ?? null)} />
          <button type="button" className={btnPrimary} onClick={() => void setNow()} title="Set the observed time to the plant clock now. The printed time stays on the record.">
            Now
          </button>
        </span>
      </label>
      {printed && printed !== value ? (
        <p className="mt-1 text-mps-micro text-mps-muted" data-printed-time="true">
          Printed time {formatStamp(printed)} is kept on the record.
        </p>
      ) : null}
      {old ? (
        <p className="mt-1 text-mps-micro text-mps-warn" role="status" data-older-than-window="true">
          This time is older than the chart window{start ? ` (starts ${formatStamp(start)})` : ""}, so the reading will not plot in place. Press Now if the device clock was wrong or the sheet is old.
        </p>
      ) : null}
    </div>
  );
}
