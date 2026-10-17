// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company
// The harness scenario choice and the running scenario survive tab changes and reloads in this browser tab.

const SCENARIO_KEY = "mps.harness.scenario";
const RUNNING_KEY = "mps.harness.running";
export const RUNNING_EVENT = "mps-harness-running";

function read(key: string): string {
  try {
    return sessionStorage.getItem(key) ?? "";
  } catch {
    return "";
  }
}

function write(key: string, value: string) {
  try {
    if (value) sessionStorage.setItem(key, value);
    else sessionStorage.removeItem(key);
  } catch {
    // Private mode: the in-memory choice still applies.
  }
}

export const readScenario = () => read(SCENARIO_KEY);
export const storeScenario = (id: string) => write(SCENARIO_KEY, id);
export const readRunning = () => read(RUNNING_KEY);
export function storeRunning(id: string) {
  write(RUNNING_KEY, id);
  window.dispatchEvent(new Event(RUNNING_EVENT));
}
