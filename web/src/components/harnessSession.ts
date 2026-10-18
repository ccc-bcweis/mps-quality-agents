// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company
// The harness scenario choice survives tab changes and reloads in this browser tab, until a reset.
// The running scenario comes from the server.

const SCENARIO_KEY = "mps.harness.scenario";
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
/**
 * The running scenario is the server's (GET /api/demo/speed, the clock message, a resync after a reset), held in
 * memory only: a value kept in this tab would outlive a reset or a stop made from another browser.
 */
let running = "";
export const readRunning = () => running;
export function storeRunning(id: string) {
  if (id === running) return;
  running = id;
  window.dispatchEvent(new Event(RUNNING_EVENT));
}

export const HARNESS_REFRESH_EVENT = "mps-harness-refresh";

/** Ask the harness to read its state from the server again (after this tab runs, stops or resets). */
export function refreshHarness() {
  window.dispatchEvent(new Event(HARNESS_REFRESH_EVENT));
}
