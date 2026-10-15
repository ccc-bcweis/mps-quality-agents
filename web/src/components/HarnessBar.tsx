// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company
import { useState } from "react";
import { Link, useLocation } from "react-router-dom";
import { api } from "../api/client";
import type { AdminSession } from "../api/types";
import { HARNESS_LINKS } from "../shell/navigation";
import { ModeBanner } from "./ModeBanner";
import { ScenarioControl } from "./ScenarioControl";
import { Term } from "./Term";
import { btn } from "./ui";

const harnessBtn =
  "inline-flex shrink-0 items-center whitespace-nowrap rounded border border-sn-gold bg-transparent px-2 py-1 font-pitch text-mps-body text-sn-gold hover:bg-sn-navy-soft focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-sn-gold disabled:cursor-not-allowed disabled:opacity-40";

export function HarnessBar({ session }: { session: AdminSession | null }) {
  const location = useLocation();
  const [resetOpen, setResetOpen] = useState(false);
  const [resetPending, setResetPending] = useState(false);
  const [resetMessage, setResetMessage] = useState<string | null>(null);

  async function confirmReset() {
    setResetPending(true);
    setResetMessage(null);
    try {
      const result = await api.resetDemo();
      setResetMessage(`Demo baseline restored. Seed ${result.seed}.`);
      setResetOpen(false);
    } catch {
      setResetMessage("The reset was not accepted.");
    } finally {
      setResetPending(false);
    }
  }

  return (
    <header
      aria-label="StatusNeo demo harness"
      data-region="harness"
      className="z-banner shrink-0 border-b-4 border-sn-gold bg-sn-navy font-pitch text-sn-gold-wash"
    >
      <div className="flex flex-wrap items-center gap-x-4 gap-y-2 px-4 py-2">
        <div className="flex shrink-0 flex-col gap-0.5">
          <span
            className="inline-block whitespace-nowrap border border-dashed border-sn-gold px-2 py-0.5 font-pitch text-mps-body font-bold tracking-wide text-sn-gold"
            title="Placeholder. Official StatusNeo artwork is not in this repo."
          >
            Powered by StatusNeo
          </span>
          <span className="whitespace-nowrap font-pitch text-mps-micro uppercase tracking-wider text-sn-gold">Demo harness</span>
        </div>
        <nav aria-label="Demo harness" className="flex flex-wrap items-center gap-1">
          {HARNESS_LINKS.map((item) => {
            const active = item.match(location.pathname);
            return (
              <Link
                key={item.to}
                to={item.to}
                aria-current={active ? "page" : undefined}
                className={`shrink-0 whitespace-nowrap rounded px-2 py-1 text-mps-body no-underline ${active ? "border border-sn-gold text-sn-gold" : "border border-transparent text-sn-gold-wash hover:text-sn-gold"}`}
              >
                {item.label === "Return on investment" ? (
                  <Term id="ROI" spellOut={false}>
                    Return on investment
                  </Term>
                ) : (
                  item.label
                )}
              </Link>
            );
          })}
        </nav>
        <ScenarioControl />
        {session?.isAdmin ? (
          <button type="button" className={harnessBtn} onClick={() => setResetOpen(true)}>
            Reset demo
          </button>
        ) : null}
        {resetMessage ? (
          <p className="basis-full text-mps-body text-sn-gold-wash" role="status">
            {resetMessage}
          </p>
        ) : null}
      </div>
      <div className="border-t border-white/10 px-4 py-2">
        <ModeBanner />
      </div>
      {resetOpen ? (
        <div className="fixed inset-0 z-toast flex items-center justify-center bg-black/60 p-4" role="presentation">
          <div role="dialog" aria-modal="true" aria-labelledby="reset-title" className="w-full max-w-lg rounded border border-sn-gold bg-sn-navy p-4 text-sn-gold-wash shadow-panel">
            <h2 id="reset-title" className="font-pitch text-mps-title font-semibold text-sn-gold">
              Reset the demo?
            </h2>
            <p className="mt-2 font-sans text-mps-body">This restores the seeded baseline. The same seed is used on this computer and in the hosted demo.</p>
            <ul className="mt-3 list-disc space-y-1 pl-5 font-sans text-mps-body">
              <li>Fleet state</li>
              <li>Tickets</li>
              <li>Investigations</li>
              <li>Approvals and corrective actions</li>
              <li>Simulated clock</li>
            </ul>
            <div className="mt-4 flex flex-wrap gap-2">
              <button type="button" className={btn} disabled={resetPending} onClick={() => void confirmReset()}>
                {resetPending ? "Resetting…" : "Confirm reset"}
              </button>
              <button type="button" className={btn} disabled={resetPending} onClick={() => setResetOpen(false)}>
                Cancel
              </button>
            </div>
          </div>
        </div>
      ) : null}
    </header>
  );
}
