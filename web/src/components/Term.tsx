// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company
import { useId, useState, type KeyboardEvent, type ReactNode } from "react";
import { createPortal } from "react-dom";
import { isAcronymTerm, lookupTerm } from "../glossary/terms";
import { useFirstUse } from "./GlossaryScope";

export function Term({ id, spellOut = true, children }: { id: string; spellOut?: boolean; children?: ReactNode }) {
  const entry = lookupTerm(id);
  const acronym = entry ? isAcronymTerm(entry.term) : false;
  const first = useFirstUse(entry?.term ?? id, spellOut && acronym && Boolean(entry));
  const tipId = useId();
  const [open, setOpen] = useState(false);
  const [box, setBox] = useState<{ top: number; left: number } | null>(null);

  if (!entry) return <>{children ?? id}</>;

  const token = children ?? entry.term;

  function place(target: HTMLElement) {
    const rect = target.getBoundingClientRect();
    const width = 280;
    const left = Math.max(8, Math.min(rect.left, window.innerWidth - width - 8));
    const below = rect.bottom + 6;
    const top = below > window.innerHeight - 140 ? Math.max(8, rect.top - 120) : below;
    setBox({ top, left });
    setOpen(true);
  }

  function show(target: HTMLElement) {
    place(target);
  }

  function hide() {
    setOpen(false);
  }

  function onKeyDown(event: KeyboardEvent<HTMLSpanElement>) {
    if (event.key === "Escape") hide();
  }

  return (
    <span
      className={first ? "mps-term" : "mps-term mps-term-token"}
      style={first ? undefined : { borderBottom: "1px dotted currentColor" }}
      tabIndex={0}
      data-term={entry.term}
      data-first-use={first ? "true" : "false"}
      aria-describedby={open ? tipId : undefined}
      onMouseEnter={(event) => show(event.currentTarget)}
      onMouseLeave={hide}
      onFocus={(event) => show(event.currentTarget)}
      onBlur={hide}
      onKeyDown={onKeyDown}
    >
      {first ? (
        <>
          {entry.expansion} (
          <span className="mps-term-token" style={{ borderBottom: "1px dotted currentColor" }}>
            {token}
          </span>
          )
        </>
      ) : (
        token
      )}
      {open && box
        ? createPortal(
            <span
              role="tooltip"
              id={tipId}
              className="mps-term-tip"
              style={{ top: box.top, left: box.left }}
            >
              <strong>{entry.expansion}</strong>
              <span>{entry.definition}</span>
              {entry.whyItMatters ? <span>Why it matters: {entry.whyItMatters}</span> : null}
            </span>,
            document.body,
          )
        : null}
    </span>
  );
}
