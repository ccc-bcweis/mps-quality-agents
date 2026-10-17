// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company
import { useEffect, useState } from "react";
import { api, ApiProblem } from "../api/client";
import type { BusinessRule, RuleSetting } from "../api/types";
import { panel } from "../components/ui";
import { useSetLifecycle } from "../context/ConsoleContext";
import { visibleProblem } from "../history/copy";
import { lifecycleRules } from "../rules/lifecycleRules";
import { PipelineMap } from "../rules/PipelineMap";
import { RuleEditor } from "../rules/RuleEditor";

export function RulesProcessPage() {
  useSetLifecycle("ticket");
  const [rules, setRules] = useState<BusinessRule[]>([]);
  const [settings, setSettings] = useState<RuleSetting[]>([]);
  const [error, setError] = useState<string | null>(null);
  const [ready, setReady] = useState(false);

  useEffect(() => {
    let cancelled = false;
    Promise.all([api.businessRules(), api.ruleSettings()])
      .then(([nextRules, nextSettings]) => {
        if (cancelled) return;
        setRules(nextRules);
        setSettings(nextSettings);
      })
      .catch((reason: unknown) => {
        if (!cancelled) setError(reason instanceof ApiProblem ? reason.title : "The rules did not load.");
      })
      .finally(() => {
        if (!cancelled) setReady(true);
      });
    return () => {
      cancelled = true;
    };
  }, []);

  const clearReadings = settings.find((setting) => setting.id === "clear.readings")?.value;
  const clearSigma = settings.find((setting) => setting.id === "clear.sigma")?.value;

  return (
    <div>
      <h1 className="text-mps-display font-semibold">Rules & Process</h1>
      <p className="max-w-3xl text-mps-secondary">
        These rules decide when an alert opens, when a ticket opens and which queue owns it, and when a person has to
        review the case. The map shows how a quality issue moves from a reading to a closed corrective action.
      </p>
      {visibleProblem(error) ? <p className="mt-3 text-mps-alarm-text">{visibleProblem(error)}</p> : null}
      {!ready && !error ? <p className="mt-3 text-mps-muted">Loading rules…</p> : null}

      {ready ? <RuleEditor rules={rules} settings={settings} lifecycle={lifecycleRules(clearReadings, clearSigma)} /> : null}

      <section aria-label="Process map" className={`${panel} mt-4`}>
        <h2 className="text-mps-title font-semibold">Process map</h2>
        <div className="mt-3">{ready ? <PipelineMap settings={settings} rules={rules} /> : null}</div>
      </section>
    </div>
  );
}
