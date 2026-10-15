// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company
import type { Alert, Incident, Investigation, Ticket } from "../api/types";

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
