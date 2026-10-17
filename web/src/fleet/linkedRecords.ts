// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company
import type { Alert, Incident, Investigation, ManualReading, Ticket } from "../api/types";

export type LinkedRecords = {
  tickets: Ticket[];
  alerts: Alert[];
  investigations: Investigation[];
};

/** Tickets, alerts and investigations that belong to one tool. Investigations link through their incident. */
export function linkedForTool(
  toolId: string,
  data: { tickets: Ticket[]; alerts: Alert[]; incidents: Incident[]; investigations: Investigation[] },
): LinkedRecords {
  const incidentIds = new Set(data.incidents.filter((incident) => incident.toolId === toolId).map((incident) => incident.id));
  for (const ticket of data.tickets) if (ticket.toolId === toolId) incidentIds.add(ticket.incidentId);
  return {
    tickets: data.tickets.filter((ticket) => ticket.toolId === toolId),
    alerts: data.alerts.filter((alert) => alert.toolId === toolId),
    investigations: data.investigations.filter((investigation) => incidentIds.has(investigation.incidentId)),
  };
}

export type LinkedEntry =
  | { kind: "investigation"; id: string; at: string; record: Investigation }
  | { kind: "ticket"; id: string; at: string; record: Ticket }
  | { kind: "alert"; id: string; at: string; record: Alert }
  | { kind: "manual"; id: string; at: string; record: ManualReading };

/** One cascade opens an alert, then a ticket, then an investigation; at the same moment the later step shows first. */
const KIND_ORDER: Record<LinkedEntry["kind"], number> = { investigation: 0, ticket: 1, alert: 2, manual: 3 };

function stamp(at: string): number {
  const value = Date.parse(at);
  return Number.isNaN(value) ? Number.NEGATIVE_INFINITY : value;
}

/** Every linked record in one list, newest first. An investigation is dated by its incident. */
export function orderedLinked(linked: LinkedRecords, incidents: Incident[], manual: ManualReading[], limit: number): LinkedEntry[] {
  const openedAt = new Map(incidents.map((incident) => [incident.id, incident.openedAt]));
  const ticketAt = new Map(linked.tickets.map((ticket) => [ticket.incidentId, ticket.openedAt]));
  const entries: LinkedEntry[] = [
    ...linked.investigations.map((record) => ({
      kind: "investigation" as const,
      id: record.id,
      at: openedAt.get(record.incidentId) ?? ticketAt.get(record.incidentId) ?? "",
      record,
    })),
    ...linked.tickets.map((record) => ({ kind: "ticket" as const, id: record.id, at: record.openedAt, record })),
    ...linked.alerts.map((record) => ({ kind: "alert" as const, id: record.id, at: record.raisedAt, record })),
    ...manual.map((record) => ({ kind: "manual" as const, id: record.evidenceId, at: record.observedAt, record })),
  ];
  return entries
    .sort((left, right) => stamp(right.at) - stamp(left.at) || KIND_ORDER[left.kind] - KIND_ORDER[right.kind])
    .slice(0, limit);
}
