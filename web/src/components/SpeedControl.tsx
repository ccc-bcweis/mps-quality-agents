// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company
import { useEffect, useState } from "react";
import { api, type DemoSpeed } from "../api/client";

const SPEEDS = [1, 2, 5, 10];
const STRIDES = [5, 15, 30, 60];
const pick = "rounded border border-sn-gold bg-sn-navy-soft px-1 py-0.5 font-sans text-mps-body text-sn-gold-wash";

/** Harness control for how fast plant data flows: simulated speed and how often a sample is kept. */
export function SpeedControl({ admin }: { admin: boolean }) {
  const [state, setState] = useState<DemoSpeed>({ speed: 1, strideSeconds: 15, speeds: SPEEDS, strides: STRIDES });
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    let cancelled = false;
    api
      .getSpeed()
      .then((next) => {
        if (!cancelled && next && typeof next.speed === "number") setState(next);
      })
      .catch(() => undefined);
    return () => {
      cancelled = true;
    };
  }, []);

  async function choose(speed: number, strideSeconds: number) {
    setError(null);
    try {
      setState(await api.setSpeed(speed, strideSeconds));
    } catch {
      setError("Speed was not changed.");
    }
  }

  return (
    <div role="group" aria-label="Data speed" className="flex flex-wrap items-center gap-2 text-mps-body">
      <label className="flex items-center gap-1 whitespace-nowrap">
        Speed
        <select aria-label="Speed" className={pick} disabled={!admin} value={state.speed} onChange={(event) => void choose(Number(event.target.value), state.strideSeconds)}>
          {(state.speeds ?? SPEEDS).map((value) => (
            <option key={value} value={value}>
              {value}x
            </option>
          ))}
        </select>
      </label>
      <label className="flex items-center gap-1 whitespace-nowrap">
        Sample every
        <select aria-label="Sample every" className={pick} disabled={!admin} value={state.strideSeconds} onChange={(event) => void choose(state.speed, Number(event.target.value))}>
          {(state.strides ?? STRIDES).map((value) => (
            <option key={value} value={value}>
              {value} s
            </option>
          ))}
        </select>
      </label>
      {error ? <span role="alert" className="text-mps-alarm-text">{error}</span> : null}
    </div>
  );
}
