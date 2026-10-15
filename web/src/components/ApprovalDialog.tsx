// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company
import { useEffect, useId, useRef, useState, type RefObject } from "react";
import { Explain } from "./Explain";
import { Term } from "./Term";
import { btn, btnDanger, btnOk, field } from "./ui";
import { APPROVE_WORD, approvalReady, rejectReady } from "../lifecycle";

export type DecisionMode = "approve" | "reject";

export function ApprovalDialog({
  mode,
  approverId,
  onCancel,
  onApprove,
  onReject,
}: {
  mode: DecisionMode;
  approverId: string;
  onCancel: () => void;
  onApprove: () => void;
  onReject: (comment: string) => void;
}) {
  const titleId = useId();
  const [word, setWord] = useState("");
  const [comment, setComment] = useState("");
  const firstField = useRef<HTMLInputElement | HTMLTextAreaElement | null>(null);
  const approving = mode === "approve";
  const ready = approving ? approvalReady(word, approverId) : rejectReady(comment);

  useEffect(() => {
    firstField.current?.focus();
  }, [mode]);

  return (
    <div className="fixed inset-0 z-overlay flex items-center justify-center bg-black/60 p-4" onClick={onCancel}>
      <div
        role="dialog"
        aria-modal="true"
        aria-labelledby={titleId}
        className="w-full max-w-lg rounded border border-mps-line bg-mps-panel p-4 shadow-overlay"
        onClick={(event) => event.stopPropagation()}
        onKeyDown={(event) => {
          if (event.key === "Escape") onCancel();
        }}
      >
        <h2 id={titleId} className="text-mps-title font-semibold">
          {approving ? "Confirm approval" : "Reject recommendation"}
        </h2>
        {approving ? (
          <>
            <p className="mt-2 text-mps-secondary">
              <Explain text="This records the approval and creates the CAPA. The console does not sign it." spellOut={false} />
            </p>
            <label className="mt-3 block text-mps-label text-mps-muted">
              Approver name
              <input ref={firstField as RefObject<HTMLInputElement>} className={field} value={approverId} readOnly title="The approver is the signed-in user." />
            </label>
            <label className="mt-3 block text-mps-label text-mps-muted">
              Type APPROVE to confirm
              <input aria-label="Type APPROVE to confirm" className={field} value={word} onChange={(event) => setWord(event.target.value)} autoComplete="off" />
            </label>
          </>
        ) : (
          <>
            <p className="mt-2 text-mps-secondary">
              <Explain text="The reason is stored with the decision and can become an eval case. No CAPA is created." spellOut={false} />
            </p>
            <label className="mt-3 block text-mps-label text-mps-muted">
              Reject reason
              <textarea
                ref={firstField as RefObject<HTMLTextAreaElement>}
                aria-label="Reject reason"
                className={field}
                value={comment}
                onChange={(event) => setComment(event.target.value)}
                rows={3}
              />
            </label>
          </>
        )}
        <div className="mt-4 flex gap-2">
          <button
            type="button"
            className={approving ? btnOk : btnDanger}
            disabled={!ready}
            onClick={() => (approving ? onApprove() : onReject(comment.trim()))}
          >
            {approving ? (
              <>
                Approve and create <Term id="CAPA" spellOut={false} />
              </>
            ) : (
              "Reject recommendation"
            )}
          </button>
          <button type="button" className={btn} onClick={onCancel}>
            Cancel
          </button>
        </div>
        {approving ? <p className="mt-2 font-mono text-mps-micro text-mps-muted">Required word: {APPROVE_WORD}</p> : null}
      </div>
    </div>
  );
}
