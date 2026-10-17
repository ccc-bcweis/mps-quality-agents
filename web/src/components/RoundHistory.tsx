// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company
import { useEffect, useState } from "react";
import { Link } from "react-router-dom";
import { api } from "../api/client";
import { CATEGORY_LABEL, INVESTIGATION_STAGE_LABEL } from "../api/labels";
import type { HumanDecisionKind, Investigation } from "../api/types";
import { Explain } from "./Explain";
import { panel } from "./ui";
import { formatLocalWhen, plainFinding } from "../investigation/format";
import { roundOf } from "./AddEvidenceBox";
import { useHubEvent } from "../live/fleetHub";

const ROUND_DECISION: Record<HumanDecisionKind, string> = {
  approve: "Approved: CAPA",
  override: "Overridden with justification",
  reject: "Rejected; ticket closed with no action",
  requestInfo: "Evidence added; the agents ran the next round",
};

/** The newest round on the ticket: the one whose buttons are live. */
export function currentRound(rounds: Investigation[]): Investigation | null {
  return rounds.reduce<Investigation | null>((best, item) => (!best || roundOf(item) > roundOf(best) ? item : best), null);
}

function topCause(round: Investigation): string | null {
  const top = round.hypotheses.slice().sort((a, b) => b.confidence - a.confidence)[0];
  if (!top) return round.escalationReason ? `No recommendation: ${plainFinding(round.escalationReason)}` : null;
  return `${CATEGORY_LABEL[top.category]}: ${plainFinding(top.cause)}`;
}

/**
 * Round 1, Round 2 and so on for one ticket. Each closed round keeps its recommendation and decision as locked
 * history; only the current round has active buttons.
 */
export function RoundHistory({ investigation }: { investigation: Investigation }) {
  const [rounds, setRounds] = useState<Investigation[]>([]);
  const [tick, setTick] = useState(0);
  const incidentId = investigation.incidentId;
  useHubEvent("investigationChanged", (payload) => {
    const next = payload as Investigation | null;
    if (next?.incidentId === incidentId) setTick((value) => value + 1);
  });
  useEffect(() => {
    let cancelled = false;
    api
      .listInvestigationRounds(incidentId)
      .then((loaded) => {
        if (!cancelled) setRounds(Array.isArray(loaded) ? loaded : []);
      })
      .catch(() => {
        // Without the list the page still shows this round.
      });
    return () => {
      cancelled = true;
    };
  }, [incidentId, investigation.id, investigation.stage, tick]);

  const all = rounds.some((item) => item.id === investigation.id) ? rounds : [...rounds, investigation];
  const current = currentRound(all);
  const viewing = roundOf(investigation);
  if (all.length <= 1 && viewing <= 1) return null;
  const ordered = all.slice().sort((a, b) => roundOf(a) - roundOf(b));

  return (
    <section aria-label="Rounds" className={`${panel} mb-3`} data-testid="round-history">
      <h2 className="mb-2 text-mps-title font-semibold">Rounds on this ticket</h2>
      {current && current.id !== investigation.id ? (
        <p className="mb-2 rounded border border-mps-warn px-3 py-2" role="status" data-testid="past-round">
          This is round {viewing}, kept as history. Its decision is final.{" "}
          <Link className="underline" to={`/investigations/${current.id}`}>
            Go to round {roundOf(current)}, the current round
          </Link>
          .
        </p>
      ) : null}
      <ol className="grid gap-2">
        {ordered.map((round) => {
          const number = roundOf(round);
          const isCurrent = current?.id === round.id;
          const isViewing = round.id === investigation.id;
          const cause = topCause(round);
          const added = round.evidence.filter((item) => item.source === "userProvided");
          return (
            <li key={round.id} className={`rounded border p-2 ${isViewing ? "border-mps-ai" : "border-mps-line"}`} data-testid={`round-${number}`}>
              <div className="flex flex-wrap items-baseline justify-between gap-2">
                <span className="font-semibold">
                  Round {number}
                  {isCurrent ? " · current" : " · locked"}
                </span>
                <span className="text-mps-secondary">{INVESTIGATION_STAGE_LABEL[round.stage]}</span>
              </div>
              {cause ? (
                <p className="mt-1">
                  <Explain text={cause} spellOut={false} />
                </p>
              ) : null}
              {round.decision ? (
                <p className="mt-1 text-mps-secondary">
                  {ROUND_DECISION[round.decision.kind] ?? round.decision.kind}
                  {round.decision.decidedBy ? ` by ${round.decision.decidedBy}` : ""}
                  {round.decision.decidedAt ? `, ${formatLocalWhen(round.decision.decidedAt)}` : ""}
                  {round.decision.note?.trim() ? `: "${round.decision.note.trim()}"` : ""}
                </p>
              ) : null}
              {round.decision?.kind !== "requestInfo" && added.length > 0 ? (
                <p className="mt-1 text-mps-micro text-mps-muted">Used {added.length === 1 ? "1 note" : `${added.length} notes`} added by a person.</p>
              ) : null}
              {isViewing ? null : (
                <Link className="mt-1 inline-block underline" to={`/investigations/${round.id}`}>
                  Open round {number}
                </Link>
              )}
            </li>
          );
        })}
      </ol>
    </section>
  );
}
