// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company
import type { Alert, Incident, Investigation, Ticket } from "../api/types";

export type MockExtras = {
  alerts: Alert[];
  tickets: Ticket[];
  incidents: Incident[];
  investigations: Investigation[];
};

const extras: MockExtras = { alerts: [], tickets: [], incidents: [], investigations: [] };

export function mockExtras(): MockExtras {
  return extras;
}

/** The harness state the mock server owns, like the API: speed, sample interval and the running scenario. */
export const mockHarness: { speed: number; strideSeconds: number; runningScenarioId: string | null } = {
  speed: 1,
  strideSeconds: 15,
  runningScenarioId: null,
};

export function resetMockState(): void {
  mockHarness.speed = 1;
  mockHarness.strideSeconds = 15;
  mockHarness.runningScenarioId = null;
  extras.alerts = [];
  extras.tickets = [];
  extras.incidents = [];
  extras.investigations = [];
}
