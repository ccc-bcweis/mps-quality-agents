// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company
import type { EvidenceItem } from "../api/types";
import { Explain } from "./Explain";
import {
  SOURCE_LABEL,
  formatLocalWhen,
  plainNarrative,
  readableExcerpt,
  valueVsSpec,
  whatItShows,
} from "../investigation/format";

function hoverRecord(item: EvidenceItem): string {
  return [item.externalRef, item.id].filter((part) => part && part.length > 0).join(" · ");
}

function fullText(item: EvidenceItem): string {
  const title = item.title.trim();
  const excerpt = item.excerpt.trim();
  if (title.length > 0 && excerpt.length > 0 && title !== excerpt) return `${title}. ${excerpt}`;
  return excerpt || title;
}

export function EvidencePack({ items }: { items: EvidenceItem[] }) {
  return (
    <div>
      <h2 className="mb-2 text-mps-title font-semibold">Evidence pack ({items.length})</h2>
      {items.length === 0 ? <p>No evidence yet.</p> : null}
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
            {items.map((item) => (
              <tr key={item.id} title={hoverRecord(item)} className="border-t border-mps-line align-top">
                <td className="break-words py-2 pr-2">{SOURCE_LABEL[item.source]}</td>
                <td className="break-words py-2 pr-2">
                  <Explain text={whatItShows(item)} spellOut={false} />
                  <details className="mt-1">
                    <summary className="cursor-pointer list-none text-mps-nominal [&::-webkit-details-marker]:hidden">Details</summary>
                    <p className="mt-1 break-words text-mps-secondary">{readableExcerpt(fullText(item))}</p>
                  </details>
                </td>
                <td className="break-words py-2 pr-2">{valueVsSpec(item)}</td>
                <td className="break-words py-2 pr-2">{formatLocalWhen(item.observedAt)}</td>
                <td className="break-words py-2 pr-2">
                  {item.whyItMatters ? <Explain text={plainNarrative(item.whyItMatters)} spellOut={false} /> : "—"}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  );
}
