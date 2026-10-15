// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company
import { useEffect, useState } from "react";
import { api, ApiProblem } from "../api/client";
import type { CausalTrace, EvidenceItem } from "../api/types";
import { formatLocalWhen } from "../investigation/format";
import { traceLines, valueVsLimit, type TraceLine } from "./tracePath";
import { btn } from "./ui";

export function TracePanel({ item, onClose }: { item: EvidenceItem; onClose: () => void }) {
  const eventId = item.sourceEventIds?.find((id) => id.trim().length > 0) ?? null;
  const [trace, setTrace] = useState<CausalTrace | null>(null);
  const [missing, setMissing] = useState(false);
  const [failed, setFailed] = useState(false);

  useEffect(() => {
    if (!eventId) return;
    let cancelled = false;
    api
      .getTrace(eventId)
      .then((body) => {
        if (!cancelled) setTrace(body);
      })
      .catch((error: unknown) => {
        if (cancelled) return;
        if (error instanceof ApiProblem && error.status === 404) setMissing(true);
        else setFailed(true);
      });
    return () => {
      cancelled = true;
    };
  }, [eventId]);

  const lines = trace ? traceLines(trace) : [];

  return (
    <div className="fixed inset-0 z-overlay flex items-start justify-center overflow-y-auto bg-black/60 p-4" onClick={onClose}>
      <div
        role="dialog"
        aria-modal="true"
        aria-label="Causal trace"
        className="mt-8 w-full max-w-xl break-words rounded border border-mps-line bg-mps-panel p-4 shadow-panel"
        onClick={(event) => event.stopPropagation()}
      >
        <div className="mb-3 flex items-start justify-between gap-3">
          <h2 className="text-mps-title font-semibold">Causal trace</h2>
          <button type="button" className={`${btn} h-auto shrink-0 whitespace-normal`} onClick={onClose}>
            Close
          </button>
        </div>
        {!eventId ? <p>The chain is not on the bus yet.</p> : null}
        {eventId && !trace && !missing && !failed ? <p>Loading the cause.</p> : null}
        {missing ? <p>The chain was not found.</p> : null}
        {failed ? <p>The cause could not be loaded.</p> : null}
        {lines.length > 0 ? (
          <ol aria-label="Cause timeline" className="space-y-2">
            {lines.map((line) => (
              <TimelineRow key={`${line.eventId}-${line.summary}`} line={line} />
            ))}
          </ol>
        ) : null}
        {lines.length > 0 ? (
          <details className="mt-4">
            <summary className="cursor-pointer text-mps-secondary">Technical details</summary>
            <ul className="mt-2 space-y-2 text-mps-secondary">
              {lines.map((line) => (
                <li key={`tech-${line.eventId}-${line.type}`} className="break-all">
                  {line.eventId} {line.type}
                </li>
              ))}
            </ul>
          </details>
        ) : null}
      </div>
    </div>
  );
}

function TimelineRow({ line }: { line: TraceLine }) {
  const limits = valueVsLimit(line);
  return (
    <li className={`break-words rounded border px-3 py-2 ${line.focus ? "border-mps-nominal" : "border-mps-line"}`}>
      <p>{line.summary}</p>
      {line.readingCount != null ? <p>{line.readingCount} readings</p> : null}
      <p className="text-mps-secondary">{formatLocalWhen(line.occurredAt)}</p>
      {line.toolName ? <p>{line.toolName}</p> : null}
      {limits ? <p>{limits}</p> : null}
    </li>
  );
}
