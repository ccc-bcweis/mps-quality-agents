// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company
import { useEffect, useState, type FormEvent } from "react";
import { useNavigate } from "react-router-dom";
import { api, ApiProblem } from "../api/client";
import { CATEGORY_LABEL } from "../api/labels";
import type { FaultInjectionRequest, FaultShape, RootCauseCategory, Tool } from "../api/types";
import { Explain } from "../components/Explain";
import { Term } from "../components/Term";
import { btnPrimary, field, panel } from "../components/ui";
import { PLANTS } from "../sites/selection";
import { sensorOptionLabel, sensorParameter, sensorsFor } from "../fleet/sensorOptions";
import { useSetLifecycle } from "../context/ConsoleContext";
import { visibleProblem } from "../history/copy";

const SHAPES: FaultShape[] = ["step", "ramp", "oscillation", "intermittent"];
const CATEGORIES = Object.keys(CATEGORY_LABEL) as RootCauseCategory[];


export function InjectorPage() {
  useSetLifecycle("alert");
  const navigate = useNavigate();
  const [tools, setTools] = useState<Tool[]>([]);
  const [toolId, setToolId] = useState("D-01");
  const [parameter, setParameter] = useState("calibrationOffset");
  const [shape, setShape] = useState<FaultShape>("ramp");
  const [magnitude, setMagnitude] = useState("2.4");
  const [category, setCategory] = useState<RootCauseCategory>("equipmentCalibrationOrAlignment");
  const [confounders, setConfounders] = useState(true);
  const [duration, setDuration] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [pending, setPending] = useState(false);

  // The injector is harness equipment: its own plant picker, independent of the console's plant filter.
  const [siteId, setSiteId] = useState("northwater");
  const toolSensors = sensorsFor(toolId);

  // Every tool at the selected plant (all three plants on All sites). Keep the choice if it is still listed.
  useEffect(() => {
    let cancelled = false;
    api
      .listTools(siteId)
      .then((next) => {
        if (cancelled) return;
        setTools(next);
        setToolId((current) => (next.some((tool) => tool.id === current) || next.length === 0 ? current : next[0].id));
      })
      .catch(() => {
        if (!cancelled) setError("The tool list did not load.");
      });
    return () => {
      cancelled = true;
    };
  }, [siteId]);

  // The parameter list is the chosen tool's own sensors, the same list the Tickets upload panel offers.
  useEffect(() => {
    const options = sensorsFor(toolId).map(sensorParameter);
    if (options.length > 0) setParameter((current) => (options.includes(current) ? current : options[0]));
  }, [toolId]);

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
        ...(Number(duration) > 0 ? { durationMinutes: Number(duration) } : {}),
      };
      await api.injectFault(body);
      navigate(tracePath(toolId, parameter));
    } catch (reason: unknown) {
      setError(reason instanceof ApiProblem ? reason.title : "The fault was not accepted.");
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
            Site
            <select className={field} value={siteId} onChange={(event) => setSiteId(event.target.value)}>
              {PLANTS.filter((plant) => plant.id !== "all").map((plant) => (
                <option key={plant.id} value={plant.id}>
                  {plant.name}
                </option>
              ))}
            </select>
          </label>
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
              {toolSensors.map((sensor) => (
                <option key={sensor.id} value={sensorParameter(sensor)}>
                  {sensorOptionLabel(sensor)}
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
            <Term id="misleading records (to rule out)">Add misleading records (to rule out)</Term>
            <select className={field} aria-label="Add misleading records (to rule out)" value={confounders ? "on" : "off"} onChange={(event) => setConfounders(event.target.value === "on")}>
              <option value="on">Yes: also write 1-2 records that look related but are not the cause</option>
              <option value="off">No: only the fault</option>
            </select>
          </label>
          <label className="mb-3 block text-mps-label text-mps-muted">
            Duration (minutes, optional — blank runs until stopped)
            <input className={field} inputMode="decimal" value={duration} onChange={(event) => setDuration(event.target.value)} />
          </label>
          <div className="flex flex-wrap gap-2">
            <button className={btnPrimary} type="submit" disabled={pending}>
              {pending ? "Injecting…" : "Inject"}
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
          <p className="mt-3 font-mono text-mps-micro text-mps-muted">One request carries one tool id.</p>
        </section>
      </div>
    </div>
  );
}
