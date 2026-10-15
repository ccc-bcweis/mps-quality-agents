// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company
import { useEffect, useState } from "react";
import { useNavigate } from "react-router-dom";
import { api, ApiProblem } from "../api/client";
import type { ScriptedScenario } from "../api/types";
import { useConsole } from "../context/ConsoleContext";
import { visibleProblem } from "../history/copy";
import { pollUntil } from "../poll";

const harnessBtn =
  "inline-flex shrink-0 items-center whitespace-nowrap rounded border border-sn-gold bg-transparent px-2 py-1 font-pitch text-mps-body text-sn-gold hover:bg-sn-navy-soft focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-sn-gold disabled:cursor-not-allowed disabled:opacity-40";

export function ScenarioControl() {
  const navigate = useNavigate();
  const { bannerMode, noteUnrecordedInjection, rememberLocalAlert } = useConsole();
  const [scenarios, setScenarios] = useState<ScriptedScenario[]>([]);
  const [scenarioId, setScenarioId] = useState("");
  const [pending, setPending] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    let cancelled = false;
    api
      .listScenarios()
      .then((next) => {
        if (cancelled || next.length === 0) return;
        setScenarios(next);
        setScenarioId((current) => current || next[0].id);
      })
      .catch(() => {
        if (!cancelled) setError("The scenario list did not load.");
      });
    return () => {
      cancelled = true;
    };
  }, []);

  const selected = scenarios.find((item) => item.id === scenarioId);

  async function runScenario() {
    if (!selected) {
      setError("Pick a named scenario.");
      return;
    }
    setPending(true);
    setError(null);
    try {
      const run = await api.runScenario(selected.id);
      const found = await pollUntil(
        () => api.listAlerts(),
        (rows) => rows.some((alert) => alert.correlationId === run.correlationId),
        { timeoutMs: 10_000, intervalMs: 400 },
      );
      if (!found) {
        rememberLocalAlert({
          id: run.faultId,
          toolId: selected.toolIds[0] ?? selected.id,
          chamberId: selected.chamberId,
          parameter: selected.parameter,
          severity: "high",
          rule: "westernElectric1",
          raisedAt: new Date().toISOString(),
          correlationId: run.correlationId,
        });
      }
      if (bannerMode === "live") noteUnrecordedInjection();
      navigate("/alerts?injected=1");
    } catch (reason: unknown) {
      setError(reason instanceof ApiProblem ? reason.title : "The scenario was not accepted.");
      setPending(false);
    }
  }

  return (
    <div className="flex max-w-full flex-wrap items-center gap-2">
      <label className="flex items-center gap-2 whitespace-nowrap text-mps-body text-sn-gold-wash">
        Scenario
        <select
          className="w-80 max-w-full rounded border border-sn-gold bg-sn-navy-soft px-2 py-1 font-sans text-mps-body text-sn-gold-wash"
          aria-label="Scenario"
          value={scenarioId}
          disabled={pending || scenarios.length === 0}
          onChange={(event) => setScenarioId(event.target.value)}
        >
          {scenarios.map((item) => (
            <option key={item.id} value={item.id}>
              {item.id} · {item.title}
            </option>
          ))}
        </select>
      </label>
      <button className={harnessBtn} type="button" disabled={pending || !selected} onClick={() => void runScenario()}>
        {pending ? "Running…" : "Run scenario"}
      </button>
      {selected ? <p className="basis-full text-mps-body text-sn-gold-wash">{selected.summary}</p> : null}
      {visibleProblem(error) ? (
        <p className="basis-full text-mps-body text-mps-alarm-text" role="alert">
          {visibleProblem(error)}
        </p>
      ) : null}
    </div>
  );
}
