// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company
import { useState } from "react";
import { api, ApiProblem } from "../api/client";
import type { PrintoutConfirmRequest, PrintoutInspection } from "../api/types";
import { ObservedAtField } from "./ObservedAtField";

const TIP =
  "Upload a bench printout. The console reads the sheet, then you confirm the reading. A reading outside the printed limits also moves the tool.";

const harnessBtn =
  "inline-flex shrink-0 items-center whitespace-nowrap rounded border border-sn-gold bg-transparent px-2 py-1 font-pitch text-mps-body text-sn-gold hover:bg-sn-navy-soft focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-sn-gold disabled:cursor-not-allowed disabled:opacity-40";

export function HarnessPrintout() {
  const [draft, setDraft] = useState<PrintoutConfirmRequest | null>(null);
  const [pending, setPending] = useState(false);
  const [note, setNote] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);

  async function onFile(file: File) {
    setPending(true);
    setError(null);
    setNote(null);
    try {
      const inspection = await api.inspectPrintout(file);
      setDraft(toDraft(inspection));
    } catch (reason: unknown) {
      setDraft(null);
      setError(reason instanceof ApiProblem ? reason.title : "The printout could not be read.");
    } finally {
      setPending(false);
    }
  }

  async function onConfirm() {
    if (!draft) return;
    setPending(true);
    setError(null);
    try {
      const confirmed = await api.confirmPrintout(draft);
      setNote(confirmed.appliedToSimulation ? "Stored the reading as a manual sample. The process-control rules decide any alert." : "Stored the reading.");
      setDraft(null);
    } catch (reason: unknown) {
      setError(reason instanceof ApiProblem ? reason.title : "The reading was not stored.");
    } finally {
      setPending(false);
    }
  }

  return (
    <div className="flex min-w-0 basis-full flex-wrap items-center gap-2" title={TIP}>
      <label className="flex min-w-0 max-w-full flex-wrap items-center gap-2 text-mps-body text-sn-gold-wash" title={TIP}>
        Sample printout
        <input
          className="max-w-full text-mps-body text-sn-gold-wash file:mr-2 file:rounded file:border file:border-sn-gold file:bg-transparent file:px-2 file:py-1 file:font-pitch file:text-mps-body file:text-sn-gold"
          type="file"
          accept="application/pdf,image/png,image/jpeg,.pdf,.png,.jpg,.jpeg"
          aria-label="Sample printout"
          title={TIP}
          disabled={pending}
          onChange={(event) => {
            const file = event.target.files?.[0];
            if (file) void onFile(file);
          }}
        />
      </label>
      {draft ? (
        <>
          <p className="whitespace-nowrap text-mps-body text-sn-gold-wash">
            Read {draft.value} {draft.unit} on {draft.toolId}.
          </p>
          <ObservedAtField
            toolId={draft.toolId}
            chamberId={draft.chamberId}
            parameter={draft.parameter}
            value={draft.observedAt}
            printed={draft.printedObservedAt}
            onChange={(observedAt, printedObservedAt) => setDraft((current) => (current ? { ...current, observedAt, printedObservedAt } : current))}
            className="rounded border border-sn-gold bg-transparent px-2 py-1 font-mono text-mps-body text-sn-gold-wash"
            labelClassName="flex flex-wrap items-center gap-2 text-mps-body text-sn-gold-wash"
          />
          <button className={harnessBtn} type="button" disabled={pending} onClick={() => void onConfirm()}>
            {pending ? "Saving…" : "Confirm reading"}
          </button>
        </>
      ) : null}
      {note ? (
        <p className="basis-full text-mps-body text-sn-gold-wash" role="status">
          {note}
        </p>
      ) : null}
      {error ? (
        <p className="basis-full text-mps-body text-mps-alarm-text" role="alert">
          {error}
        </p>
      ) : null}
    </div>
  );
}

function toDraft(inspection: PrintoutInspection): PrintoutConfirmRequest {
  return {
    toolId: inspection.toolId,
    chamberId: inspection.chamberId,
    observedAt: inspection.observedAt,
    instrument: inspection.instrument,
    parameter: inspection.parameter,
    value: inspection.value,
    unit: inspection.unit,
    specMinimum: inspection.specMinimum,
    specMaximum: inspection.specMaximum,
    shape: inspection.shape,
    operatorInitials: inspection.operatorInitials,
    fileId: inspection.fileId ?? null,
  };
}
