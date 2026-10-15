// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company
import { useEffect, useState } from "react";
import { useParams } from "react-router-dom";
import { api, ApiProblem } from "../api/client";
import { btnPrimary, panel } from "../components/ui";

/** Opened from a join link. Easy Auth has already signed the visitor in; this spends the link and adds them to the demo. */
export function JoinPage() {
  const { token = "" } = useParams();
  const [state, setState] = useState<{ ok: boolean; text: string } | null>(null);
  useEffect(() => {
    let cancelled = false;
    api
      .redeemJoin(token)
      .then((result) => {
        if (!cancelled) setState({ ok: true, text: result.message });
      })
      .catch((reason: unknown) => {
        if (!cancelled) setState({ ok: false, text: reason instanceof ApiProblem ? reason.title : "The join link did not work." });
      });
    return () => {
      cancelled = true;
    };
  }, [token]);
  return (
    <div className="flex min-h-full items-center justify-center bg-mps-canvas p-6">
      <section className={`${panel} max-w-lg`}>
        <h1 className="text-mps-display font-semibold">Join the MPS quality demo</h1>
        <p className="mt-3" role="status">
          {state ? state.text : "Checking your link…"}
        </p>
        {state?.ok ? (
          <a className={`${btnPrimary} mt-4`} href="/">
            Open the console
          </a>
        ) : null}
      </section>
    </div>
  );
}
