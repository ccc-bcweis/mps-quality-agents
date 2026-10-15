// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company
import { useEffect, useState, type ReactNode } from "react";
import { useNavigate } from "react-router-dom";
import { api, ApiProblem } from "../api/client";
import type { ScriptedScenario } from "../api/types";
import { lookupTerm } from "../glossary/terms";
import { Explain } from "./Explain";
import { Term } from "./Term";

/** Acronyms go through Explain. The card also links the plain glossary phrases in the story. */
function CardCopy({ text }: { text: string }) {
  const nodes: ReactNode[] = [];
  let cursor = 0;
  for (const match of text.matchAll(/foreline pump|edge ring|precursor/gi)) {
    const index = match.index ?? 0;
    if (index > cursor) nodes.push(<Explain key={`t-${cursor}`} text={text.slice(cursor, index)} />);
    const word = match[0];
    const term = lookupTerm(word);
    nodes.push(
      term ? (
        <Term key={`p-${index}`} id={term.term}>
          {word}
        </Term>
      ) : (
        <span key={`p-${index}`}>{word}</span>
      ),
    );
    cursor = index + word.length;
  }
  if (cursor < text.length) nodes.push(<Explain key={`t-${cursor}`} text={text.slice(cursor)} />);
  if (nodes.length === 0) return <Explain text={text} />;
  return <>{nodes}</>;
}
import { visibleProblem } from "../history/copy";

const harnessBtn =
  "inline-flex shrink-0 items-center whitespace-nowrap rounded border border-sn-gold bg-transparent px-2 py-1 font-pitch text-mps-body text-sn-gold hover:bg-sn-navy-soft focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-sn-gold disabled:cursor-not-allowed disabled:opacity-40";

export function ScenarioControl() {
  const navigate = useNavigate();
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
    const toolId = selected.toolIds[0];
    if (!toolId) {
      setError("This scenario has no tool.");
      return;
    }
    setPending(true);
    setError(null);
    try {
      await api.runScenario(selected.id);
      const parameter = encodeURIComponent(selected.parameter);
      navigate(`/tools/${encodeURIComponent(toolId)}?streaming=1&parameter=${parameter}`);
    } catch (reason: unknown) {
      setError(reason instanceof ApiProblem ? reason.title : "The scenario was not accepted.");
    } finally {
      setPending(false);
    }
  }

  const happening = selected?.whatsHappening || selected?.summary || "";
  const simulate = selected?.whatWeSimulate || selected?.summary || "";
  const finding = selected?.whatAiShouldFind || "";
  const cause = selected?.expectedCause || "";

  return (
    <div className="flex min-w-0 max-w-full basis-full flex-wrap items-center gap-2">
      <label className="flex min-w-0 items-center gap-2 whitespace-nowrap text-mps-body text-sn-gold-wash">
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
      {selected ? (
        <section
          aria-label={`${selected.id} scenario card`}
          className="basis-full min-w-0 max-w-full break-words rounded border border-sn-gold/40 bg-sn-navy-soft px-3 py-2 text-mps-body text-sn-gold-wash"
        >
          <h2 className="break-words text-mps-body font-semibold">
            {selected.id} · {selected.title}
          </h2>
          <p className="mt-1 break-words">
            <span className="font-semibold">What&apos;s happening. </span>
            <CardCopy text={happening} />
          </p>
          <p className="mt-1 break-words">
            <span className="font-semibold">What data we simulate. </span>
            <CardCopy text={simulate} />
          </p>
          <details className="mt-1 min-w-0 max-w-full break-words">
            <summary className="cursor-pointer">What the AI should find</summary>
            {finding ? (
              <p className="mt-1 break-words">
                <CardCopy text={finding} />
              </p>
            ) : null}
            {cause ? (
              <p className="mt-1 break-words">
                Expected cause: <CardCopy text={cause} />
              </p>
            ) : null}
          </details>
        </section>
      ) : null}
      {visibleProblem(error) ? (
        <p className="basis-full text-mps-body text-mps-alarm-text" role="alert">
          {visibleProblem(error)}
        </p>
      ) : null}
    </div>
  );
}
