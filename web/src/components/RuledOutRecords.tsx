// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company
import type { EvidenceItem, RuledOutItem } from "../api/types";
import { plainFinding, plainNarrative } from "../investigation/format";
import { ruledOutViews } from "../investigation/ruledOut";
import { Explain } from "./Explain";

/** Title and reason for each ruled-out record. Hidden when the list is empty or missing. */
export function RuledOutRecords({
  items,
  ruledOut,
  showSource = false,
  label = "Ruled out records",
}: {
  items: readonly EvidenceItem[];
  ruledOut?: readonly RuledOutItem[] | null;
  showSource?: boolean;
  label?: string;
}) {
  const rows = ruledOutViews(items, ruledOut);
  if (rows.length === 0) return null;
  return (
    <ul aria-label={label} className="mt-1 space-y-1">
      {rows.map((row) => (
        <li key={row.id}>
          {row.ref ? `${row.ref}: ` : ""}
          {row.title}
          {row.reason ? (
            <>
              {". "}
              <Explain text={plainFinding(row.reason)} spellOut={false} />
            </>
          ) : null}
          {showSource && row.sourceLabel ? <div className="text-mps-secondary">{row.sourceLabel}</div> : null}
        </li>
      ))}
    </ul>
  );
}

/**
 * Counter-evidence, then the ruled-out records. The ruled-out list is hidden when empty.
 * The whole block is hidden when both are empty.
 */
export function EvidenceAgainst({
  counterEvidence,
  items,
  ruledOut,
}: {
  counterEvidence?: readonly string[] | null;
  items: readonly EvidenceItem[];
  ruledOut?: readonly RuledOutItem[] | null;
}) {
  const lines = counterEvidence ?? [];
  const rows = ruledOutViews(items, ruledOut);
  if (lines.length === 0 && rows.length === 0) return null;
  return (
    <div className="mt-1">
      {lines.length > 0 ? (
        <p>
          <strong>Evidence against this:</strong> {lines.map((line) => plainNarrative(line)).join(" ")}
        </p>
      ) : null}
      <RuledOutRecords items={items} ruledOut={ruledOut} />
    </div>
  );
}
