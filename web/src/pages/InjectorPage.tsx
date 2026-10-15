// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company
import { useEffect, useState, type FormEvent } from "react";
import { useNavigate } from "react-router-dom";
import { api, ApiProblem } from "../api/client";
import { CATEGORY_LABEL } from "../api/labels";
import type { FaultInjectionRequest, FaultShape, RootCauseCategory, Tool } from "../api/types";
import { Explain } from "../components/Explain";
import { btn, btnPrimary, field, panel } from "../components/ui";
import { useSetLifecycle } from "../context/ConsoleContext";
import { visibleProblem } from "../history/copy";

const SHAPES: FaultShape[] = ["step", "ramp", "oscillation", "intermittent"];
const CATEGORIES = Object.keys(CATEGORY_LABEL) as RootCauseCategory[];

const PARAMETERS = [
  { id: "chamberPressure", label: "Chamber pressure" },
  { id: "calibrationOffset", label: "Calibration (mass flow controller / manometer)" },
  { id: "escTemperature", label: "Electrostatic chuck / heater temperature" },
  { id: "particleAdders", label: "Particle adders" },
  { id: "gasFlow", label: "Gas / chem flow" },
];

export function InjectorPage() {
  useSetLifecycle("alert");
  const navigate = useNavigate();
  const [tools, setTools] = useState<Tool[]>([]);
  const [toolId, setToolId] = useState("S-01");
  const [parameter, setParameter] = useState("calibrationOffset");
  const [shape, setShape] = useState<FaultShape>("ramp");
  const [magnitude, setMagnitude] = useState("2.4");
  const [category, setCategory] = useState<RootCauseCategory>("equipmentCalibrationOrAlignment");
  const [confounders, setConfounders] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [pending, setPending] = useState(false);

  useEffect(() => {
    let cancelled = false;
    api
      .listTools()
      .then((next) => {
        if (!cancelled) setTools(next);
      })
      .catch(() => {
        if (!cancelled) setError("The tool list did not load.");
      });
    return () => {
      cancelled = true;
    };
  }, []);

  function tracePath(nextToolId: string, nextParameter: string): string {
    return `/tools/${encodeURIComponent(nextToolId)}?streaming=1&parameter=${encodeURIComponent(nextParameter)}`;
  }

  async function onSubmit(event: FormEvent) {
    event.preventDefault();
    const magnitudeValue = Number(magnitude);
    if (!Number.isFinite(magnitudeValue)) {
      setError("Magnitude has to be a number, in sigma or percent.");
      return;
    }
    setPending(true);
    setError(null);
    try {
      const body: FaultInjectionRequest = {
        toolId,
        parameter,
        shape,
        magnitude: magnitudeValue,
        rootCauseCategory: category,
        confounders,
      };
      await api.injectFault(body);
      navigate(tracePath(toolId, parameter));
    } catch (reason: unknown) {
      setError(reason instanceof ApiProblem ? reason.title : "The fault was not accepted.");
    } finally {
      setPending(false);
    }
  }

  async function presetS4() {
    setPending(true);
    setError(null);
    try {
      for (const etchTool of ["E-01", "E-02"]) {
        await api.injectFault({
          toolId: etchTool,
          parameter: "gasFlow",
          shape: "step",
          magnitude: 2.4,
          rootCauseCategory: "supplyChainMaterial",
          confounders: true,
        });
      }
      navigate(tracePath("E-01", "gasFlow"));
    } catch (reason: unknown) {
      setError(reason instanceof ApiProblem ? reason.title : "The preset was not accepted.");
    } finally {
      setPending(false);
    }
  }

  return (
    <div>
      <div className="mb-4">
        <h1 className="text-mps-display font-semibold">Fault injector</h1>
        <p className="max-w-3xl break-words text-mps-secondary">
          Throw a new problem onto the live trace. Agents do not receive the fault object. The category below is held out of the alert. Named scenarios run from the demo harness bar. This click does not open an alert. The trace has to cross a rule first.
        </p>
      </div>
      {visibleProblem(error) ? (
        <p className="mb-3 text-mps-alarm-text" role="alert">
          {visibleProblem(error)}
        </p>
      ) : null}
      <div className="grid gap-3 lg:grid-cols-2">
        <form className={panel} onSubmit={onSubmit}>
          <h2 className="mb-2 text-mps-title font-semibold">Inject</h2>
          <label className="mb-3 block text-mps-label text-mps-muted">
            Tool
            <select className={field} value={toolId} onChange={(event) => setToolId(event.target.value)}>
              {tools.map((tool) => (
                <option key={tool.id} value={tool.id}>
                  {tool.id} · {tool.model} {tool.family}
                </option>
              ))}
            </select>
          </label>
          <label className="mb-3 block text-mps-label text-mps-muted">
            Parameter
            <select className={field} value={parameter} onChange={(event) => setParameter(event.target.value)}>
              {PARAMETERS.map((item) => (
                <option key={item.id} value={item.id}>
                  {item.label}
                </option>
              ))}
            </select>
          </label>
          <label className="mb-3 block text-mps-label text-mps-muted">
            Shape
            <select className={field} value={shape} onChange={(event) => setShape(event.target.value as FaultShape)}>
              {SHAPES.map((item) => (
                <option key={item} value={item}>
                  {item}
                </option>
              ))}
            </select>
          </label>
          <label className="mb-3 block text-mps-label text-mps-muted">
            Magnitude (σ)
            <input className={field} inputMode="decimal" value={magnitude} onChange={(event) => setMagnitude(event.target.value)} required />
          </label>
          <label className="mb-3 block text-mps-label text-mps-muted">
            Root-cause type (held out of agents)
            <select className={field} value={category} onChange={(event) => setCategory(event.target.value as RootCauseCategory)}>
              {CATEGORIES.map((item) => (
                <option key={item} value={item}>
                  {CATEGORY_LABEL[item]}
                </option>
              ))}
            </select>
          </label>
          <label className="mb-3 block text-mps-label text-mps-muted">
            Confounders
            <select className={field} value={confounders ? "on" : "off"} onChange={(event) => setConfounders(event.target.value === "on")}>
              <option value="on">on</option>
              <option value="off">off</option>
            </select>
          </label>
          <div className="flex flex-wrap gap-2">
            <button className={btnPrimary} type="submit" disabled={pending}>
              {pending ? "Injecting…" : "Inject"}
            </button>
            <button className={btn} type="button" disabled={pending} onClick={() => void presetS4()}>
              Preset S4: gas lot on etch tools
            </button>
          </div>
        </form>
        <section className={panel}>
          <h2 className="mb-2 text-mps-title font-semibold">What happens</h2>
          <ol className="list-decimal space-y-1 pl-5">
            <li>The simulator applies the signal shape on the selected tool and parameter.</li>
            <li>The tool page shows the streaming trace. Readings move over the next ticks.</li>
            <li>
              <Explain text="SPC rules open an alert when the points cross the limit, then a ticket." />
            </li>
            <li>The investigation starts from that alert. Nothing is staged on this click.</li>
          </ol>
          <p className="mt-3 font-mono text-mps-micro text-mps-muted">One request carries one tool id. The S4 preset sends E-01 and E-02.</p>
        </section>
      </div>
    </div>
  );
}
