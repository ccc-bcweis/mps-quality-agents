// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company
import { useState } from "react";
import type { EvidenceItem, RuledOutItem } from "../api/types";
import { Explain } from "./Explain";
import { RuledOutRecords } from "./RuledOutRecords";
import { TracePanel } from "./TracePanel";
import { btn } from "./ui";
import {
  SOURCE_LABEL,
  formatLocalWhen,
  plainNarrative,
  readableExcerpt,
  valueVsSpec,
  whatItShows,
} from "../investigation/format";
import { groupEvidence, rangeText } from "../investigation/evidenceGroups";
import { ruledOutViews } from "../investigation/ruledOut";
import { LIMIT_LABEL, limitClass } from "../investigation/limits";

function hoverRecord(item: EvidenceItem): string {
  return [item.externalRef, item.id].filter((part) => part && part.length > 0).join(" · ");
}

function fullText(item: EvidenceItem): string {
  const title = item.title.trim();
  const excerpt = item.excerpt.trim();
  if (title.length > 0 && excerpt.length > 0 && title !== excerpt) return `${title}. ${excerpt}`;
  return excerpt || title;
}

export function EvidencePack({ items, ruledOut }: { items: EvidenceItem[]; ruledOut?: readonly RuledOutItem[] | null }) {
  const [traceItem, setTraceItem] = useState<EvidenceItem | null>(null);
  const dismissed = new Set(ruledOutViews(items, ruledOut).map((row) => row.id));
  const supporting = items.filter((item) => !dismissed.has(item.id));
  const rows = groupEvidence(supporting);
  const collapsed = rows.length !== supporting.length;
  return (
    <div>
      <h2 className="mb-2 text-mps-title font-semibold">
        Evidence pack ({collapsed ? `${rows.length} signals from ${items.length} records` : items.length})
      </h2>
      {items.length === 0 ? <p>No evidence yet.</p> : null}
      {rows.length === 0 ? null : (
      <div className="overflow-x-auto">
        <table aria-label="Evidence pack" className="w-full table-fixed text-left">
          <thead>
            <tr className="text-mps-label text-mps-muted">
              <th className="w-[14%] py-1 pr-2 font-medium">Source</th>
              <th className="w-[30%] py-1 pr-2 font-medium">What it shows</th>
              <th className="w-[18%] py-1 pr-2 font-medium">Value vs spec</th>
              <th className="w-[16%] py-1 pr-2 font-medium">When</th>
              <th className="w-[22%] py-1 pr-2 font-medium">Why it matters</th>
            </tr>
          </thead>
          <tbody>
            {rows.map((row) => {
              const item = row.item;
              const range = rangeText(row);
              return (
              <tr key={item.id} title={hoverRecord(item)} className="border-t border-mps-line align-top">
                <td className="break-words py-2 pr-2">
                  {SOURCE_LABEL[item.source]}
                  {item.source === "userProvided" ? (
                    <div className="mt-1 inline-block rounded border border-mps-warn px-1 text-mps-micro font-semibold" data-testid="user-provided">
                      User-provided{item.suppliedBy ? `: ${item.suppliedBy}` : ""}
                    </div>
                  ) : null}
                </td>
                <td className="break-words py-2 pr-2">
                  {item.source === "userProvided" ? (
                    <p className="whitespace-pre-line break-words">{item.excerpt}</p>
                  ) : (
                    <Explain text={whatItShows(item)} spellOut={false} />
                  )}
                  {range ? <div className="text-mps-micro text-mps-muted">{range}</div> : null}
                  {row.rules.length > 1 ? (
                    <div className="text-mps-micro text-mps-muted">
                      <Explain text={`Rules: ${row.rules.join("; ")}`} spellOut={false} />
                    </div>
                  ) : null}
                  <details className="mt-1">
                    <summary className={`${btn} inline-flex list-none [&::-webkit-details-marker]:hidden`}>Details</summary>
                    <p className="mt-1 break-words text-mps-secondary">{readableExcerpt(fullText(item))}</p>
                  </details>
                  {item.source === "userProvided" ? null : (
                    <button
                      type="button"
                      className={`${btn} mt-2 h-auto w-full whitespace-normal break-words text-left`}
                      onClick={() => setTraceItem(item)}
                    >
                      Show the cause for {whatItShows(item)}
                    </button>
                  )}
                </td>
                <td className="break-words py-2 pr-2">
                  {valueVsSpec(item)}
                  {item.limitState ? (
                    <div className={`text-mps-micro font-semibold ${limitClass(item.limitState)}`} data-testid="limit-state">
                      <Explain text={LIMIT_LABEL[item.limitState]} spellOut={false} />
                    </div>
                  ) : null}
                </td>
                <td className="break-words py-2 pr-2">{row.count > 1 ? `${formatLocalWhen(row.first)} – ${formatLocalWhen(row.last)}` : formatLocalWhen(item.observedAt)}</td>
                <td className="break-words py-2 pr-2">
                  {item.whyItMatters ? <Explain text={plainNarrative(item.whyItMatters)} spellOut={false} /> : "—"}
                </td>
              </tr>
              );
            })}
          </tbody>
        </table>
      </div>
      )}
      {dismissed.size > 0 ? (
        <section aria-label="Considered and ruled out" className="mt-4">
          <h3 className="mb-1 font-semibold">Considered and ruled out</h3>
          <RuledOutRecords items={items} ruledOut={ruledOut} showSource label="Considered and ruled out" />
        </section>
      ) : null}
      {traceItem ? <TracePanel item={traceItem} onClose={() => setTraceItem(null)} /> : null}
    </div>
  );
}
