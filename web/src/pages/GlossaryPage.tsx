// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company
import { useMemo, useState } from "react";
import { useSearchParams } from "react-router-dom";
import { Term } from "../components/Term";
import { panel } from "../components/ui";
import { useSetLifecycle } from "../context/ConsoleContext";
import { glossaryNote, glossarySource, glossaryTerms } from "../glossary/terms";

export function GlossaryPage() {
  useSetLifecycle("alert");
  // A link such as /glossary?q=recipe%20step opens the page filtered to that term.
  const [params] = useSearchParams();
  const [query, setQuery] = useState(() => params.get("q") ?? "");
  const terms = useMemo(
    () => [...glossaryTerms].sort((a, b) => a.term.localeCompare(b.term, "en", { sensitivity: "base" })),
    [],
  );
  const needle = query.trim().toLowerCase();
  const visible = needle
    ? terms.filter((term) =>
        [term.term, term.expansion, term.definition, term.whyItMatters ?? ""].join(" ").toLowerCase().includes(needle),
      )
    : terms;

  return (
    <div>
      <div className="mb-4">
        <h1 className="text-mps-display font-semibold">Glossary</h1>
        <p className="max-w-3xl text-mps-secondary">
          Short terms are spelled out the first time they appear on a screen. Every later use keeps a dotted underline.
          Hover or focus the term for the plain definition. This page lists the whole file.
        </p>
        {glossaryNote ? <p className="mt-2 text-mps-micro text-mps-warn">{glossaryNote}</p> : null}
        <p className="mt-1 font-mono text-mps-micro text-mps-muted">Loaded from {glossarySource}</p>
      </div>
      <label className="mb-3 block text-mps-label text-mps-muted">
        Filter
        <input
          className="mt-1 w-full max-w-md rounded border border-mps-line bg-mps-canvas px-2 py-1.5 text-mps-body text-mps-text"
          value={query}
          aria-label="Filter glossary"
          onChange={(event) => setQuery(event.target.value)}
        />
      </label>
      <div className={`${panel} overflow-x-auto`}>
        <table className="w-full text-left">
          <thead>
            <tr className="font-mono text-mps-micro uppercase text-mps-muted">
              <th className="py-1 font-medium">Term</th>
              <th className="py-1 font-medium">Spelled out</th>
              <th className="py-1 font-medium">Plain definition</th>
              <th className="py-1 font-medium">Why it matters</th>
            </tr>
          </thead>
          <tbody>
            {visible.map((term) => (
              <tr key={term.term} className="border-t border-mps-line align-top">
                <td className="py-2 pr-3 font-mono">
                  <Term id={term.term} spellOut={false} />
                </td>
                <td className="py-2 pr-3">{term.expansion}</td>
                <td className="py-2 pr-3 text-mps-secondary">{term.definition}</td>
                <td className="py-2 text-mps-secondary">{term.whyItMatters ?? "—"}</td>
              </tr>
            ))}
          </tbody>
        </table>
        {visible.length === 0 ? <p className="mt-3 text-mps-muted">No terms match that filter.</p> : null}
      </div>
    </div>
  );
}
