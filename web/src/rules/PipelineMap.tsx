// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company
import type { ReactNode } from "react";
import type { BusinessRule, RuleSetting } from "../api/types";
import { Explain } from "../components/Explain";
import { CAPA_STAGES } from "../labels";

/** Small line pictograms, one per station. Stroke only, so they follow the text colour. */
const ICONS: Record<string, ReactNode> = {
  readings: (
    <>
      <path d="M3 17c2-6 4-6 6 0s4 6 6 0 4-6 6 0" />
      <rect x="5" y="3" width="6" height="7" rx="1" />
      <path d="M14 4h6M14 7h6M14 10h4" />
    </>
  ),
  rules: (
    <>
      <path d="M3 5h18M3 12h18M3 19h18" strokeDasharray="2 2" />
      <path d="M3 14l4-3 3 4 4-9 3 6 4-2" />
    </>
  ),
  alert: (
    <>
      <path d="M6 16V11a6 6 0 0 1 12 0v5l2 2H4z" />
      <path d="M10 20a2 2 0 0 0 4 0" />
    </>
  ),
  ticket: (
    <>
      <path d="M3 7h18v3a2 2 0 0 0 0 4v3H3v-3a2 2 0 0 0 0-4z" />
      <path d="M9 7v10" strokeDasharray="2 2" />
    </>
  ),
  investigation: (
    <>
      <circle cx="10" cy="10" r="6" />
      <path d="M15 15l6 6" />
    </>
  ),
  decision: (
    <>
      <path d="M12 3v18M5 7h14" />
      <path d="M5 7l-3 7h6zM19 7l-3 7h6z" />
    </>
  ),
  capa: (
    <>
      <rect x="5" y="4" width="14" height="17" rx="1" />
      <path d="M9 4V2h6v2M8 10h8M8 14h8M8 18h5" />
    </>
  ),
  containment: (
    <>
      <path d="M12 3l8 3v6c0 5-4 8-8 9-4-1-8-4-8-9V6z" />
      <path d="M8 12l3 3 5-6" />
    </>
  ),
  close: (
    <>
      <circle cx="12" cy="12" r="9" />
      <path d="M7 12l3 3 7-7" />
    </>
  ),
};

function Icon({ id }: { id: string }) {
  return (
    <svg viewBox="0 0 24 24" width="28" height="28" fill="none" stroke="currentColor" strokeWidth="1.6" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
      {ICONS[id]}
    </svg>
  );
}

function Arrow({ label }: { label?: string }) {
  return (
    <div className="flex items-center gap-2 py-1 pl-5 text-mps-muted" aria-hidden="true">
      <svg viewBox="0 0 12 24" width="12" height="24" fill="none" stroke="currentColor" strokeWidth="1.6">
        <path d="M6 0v20M2 16l4 6 4-6" />
      </svg>
      {label ? <span className="text-mps-micro">{label}</span> : null}
    </div>
  );
}

type Station = { id: string; title: string; text: string; parts?: string[]; branches?: { label: string; result: string; tone: "ok" | "info" | "warn" | "alarm" }[]; next?: string };

const TONE = {
  ok: "border-mps-ok text-mps-ok",
  info: "border-mps-nominal text-mps-nominal",
  warn: "border-mps-warn text-mps-warn",
  alarm: "border-mps-alarm text-mps-alarm-text",
} as const;

/** The stations of the real flow, filled from the rule settings and business rules the server is running with. */
export function pipelineStations(settings: RuleSetting[], rules: BusinessRule[]): Station[] {
  // Every number is the running value from the server. A setting that did not load shows a dash, never a guess.
  const value = (id: string) => settings.find((setting) => setting.id === id)?.value ?? "—";
  const on = (id: string) => settings.find((setting) => setting.id === id)?.value === "true";
  const brief = (id: string) => settings.find((setting) => setting.id === id)?.help?.replace(/\.$/, "") ?? "—";
  const ruleParts = [
    on("spc.we1.on") ? `Western Electric rule 1: ${brief("spc.we1.on")}` : null,
    on("spc.we2.on") ? `Western Electric rule 2: ${brief("spc.we2.on")}` : null,
    on("spc.we3.on") ? `Western Electric rule 3: ${brief("spc.we3.on")}` : null,
    on("spc.we4.on") ? `Western Electric rule 4: ${brief("spc.we4.on")}` : null,
    on("spc.nelson.on") ? `Nelson rule 6: ${brief("spc.nelson.on")}` : null,
  ].filter((part): part is string => part !== null);
  const ticketRules = rules.filter((rule) => rule.actions.some((action) => action.type === "open-ticket"));
  const queues = [...new Set(ticketRules.flatMap((rule) => rule.actions.filter((action) => action.type === "route-to-queue" && action.queue).map((action) => action.queue as string)))];
  const waits = on("investigation.wait.on");
  const capaStages = CAPA_STAGES.map((stage) => stage.label).join(", then ");
  return [
    {
      id: "readings",
      title: "Readings come in",
      text: "Sensor telemetry from each tool, a Manual reading typed in, or a scanned printout. All three join the same series for that tool and parameter.",
    },
    {
      id: "rules",
      title: "Rules check every reading",
      text: `Each reading is compared with the recipe spec for the step that was running and with SPC control limits at ${value("spc.controlLimitSigma")} sigma. These patterns count as a rule break:`,
      parts: ruleParts,
    },
    {
      id: "alert",
      title: "One alert per tool and parameter",
      text: `A repeat break updates the same alert instead of opening a new one. The tool shows watch when a rule breaks or a reading is past ${value("spc.watchSigma")} sigma while still inside the recipe spec, and alarm once a reading is outside the recipe spec. At ${value("spc.criticalSigma")} sigma the alert is critical.`,
    },
    {
      id: "ticket",
      title: "A business rule opens a ticket",
      text: `${ticketRules.length} rule${ticketRules.length === 1 ? "" : "s"} open a ticket, set its priority and pick the queue${queues.length > 0 ? ` (${queues.join(", ")})` : ""}. The first rule that matches wins.`,
    },
    {
      id: "investigation",
      title: "The investigation gathers evidence",
      text: waits
        ? `It waits until ${value("investigation.wait.minReadings")} readings have arrived since the first rule break, so the signal shape is clear. Then:`
        : "It starts at once. Then:",
      parts: [
        "Triage plans which records to read",
        "Evidence is pulled read-only from each system",
        "Possible causes are ranked against that evidence",
        "The evidence gate checks the evidence is enough and the records agree; if not, the case goes to a person",
      ],
    },
    {
      id: "decision",
      title: "A person decides",
      text: "The engineer reads the recommendation and the evidence and picks one. A decision is final for that round.",
      branches: [
        { label: "Approve CAPA", result: "opens the CAPA", tone: "ok" },
        {
          label: "Request more evidence",
          result: `the engineer adds evidence, the round closes, and the agents run the next round with it, up to ${value("decision.maxEvidenceRounds")} rounds per ticket`,
          tone: "info",
        },
        { label: "Override with justification", result: `only for an escalated case, with at least ${value("decision.overrideMin")} characters of reason; opens the CAPA`, tone: "warn" },
        { label: "Reject", result: "no CAPA; the ticket closes with no action and the case stays on record", tone: "alarm" },
      ],
    },
    {
      id: "capa",
      title: "The CAPA tracks the fix",
      text: `Stages: ${capaStages}.`,
    },
    {
      id: "containment",
      title: "Containment clears the alarm",
      text: `Moving the CAPA to Containment removes the fault. The alert clears after ${value("clear.readings")} readings in a row within ${value("clear.sigma")} sigma, and the tool goes back to nominal.`,
    },
    {
      id: "close",
      title: "Close",
      text: "Verification is allowed only when no alert on that signal is still active. Closing the CAPA closes the incident and its ticket.",
    },
  ];
}

/** One pictographic map of the actual flow, from readings to close. */
export function PipelineMap({ settings, rules }: { settings: RuleSetting[]; rules: BusinessRule[] }) {
  const stations = pipelineStations(settings, rules);
  return (
    <ol aria-label="Quality flow" className="m-0 list-none p-0">
      {stations.map((station, index) => (
        <li key={station.id} data-stage={station.id}>
          {index > 0 ? <Arrow label={station.id === "capa" ? "approved or overridden" : undefined} /> : null}
          <div className="flex items-start gap-3 rounded border border-mps-line bg-mps-raised p-3">
            <div className="flex h-11 w-11 shrink-0 items-center justify-center rounded-full border border-mps-nominal/60 text-mps-nominal">
              <Icon id={station.id} />
            </div>
            <div className="min-w-0">
              <h3 className="font-semibold">
                <span className="mr-2 font-mono text-mps-micro text-mps-muted">{String(index + 1).padStart(2, "0")}</span>
                <Explain text={station.title} spellOut={false} />
              </h3>
              <p className="mt-1 text-mps-secondary">
                <Explain text={station.text} spellOut={false} />
              </p>
              {station.parts ? (
                <ul className="mt-1 list-disc pl-5">
                  {station.parts.map((part) => (
                    <li key={part}>
                      <Explain text={part} spellOut={false} />
                    </li>
                  ))}
                </ul>
              ) : null}
              {station.branches ? (
                <ul aria-label="Decision choices" className="mt-2 grid gap-2 sm:grid-cols-2">
                  {station.branches.map((branch) => (
                    <li key={branch.label} className={`rounded border px-2 py-1 ${TONE[branch.tone]}`}>
                      <span className="font-semibold">
                        <Explain text={branch.label} spellOut={false} />
                      </span>
                      <span className="text-mps-secondary"> → {branch.result}</span>
                    </li>
                  ))}
                </ul>
              ) : null}
            </div>
          </div>
        </li>
      ))}
    </ol>
  );
}
