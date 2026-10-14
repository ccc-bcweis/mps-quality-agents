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

export function resetMockState(): void {
  extras.alerts = [];
  extras.tickets = [];
  extras.incidents = [];
  extras.investigations = [];
}
