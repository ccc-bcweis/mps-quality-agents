// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

export type InvestigationRef = {
  id: string;
  incidentId: string;
};

export type TicketRef = {
  id: string;
  number: string;
  incidentId: string;
};

/**
 * Last investigation for an incident.
 * `investigations` is oldest first. A rejected investigation still counts.
 */
export function latestInvestigationForIncident(
  incidentId: string,
  investigations: readonly InvestigationRef[],
): InvestigationRef | undefined {
  let latest: InvestigationRef | undefined;
  for (const item of investigations) {
    if (item.incidentId === incidentId) latest = item;
  }
  return latest;
}

/** Route for an incident. An investigation id is used only when one already exists. */
export function pathForIncident(incidentId: string, investigations: readonly InvestigationRef[] = []): string {
  const latest = latestInvestigationForIncident(incidentId, investigations);
  if (latest) return `/investigations/${latest.id}`;
  return `/investigations/incident/${incidentId}`;
}

/**
 * Route for a ticket. The ticket id and the ticket number are never the route id.
 * The incident id is. The page resolves the latest investigation, or offers to start one.
 */
export function pathForTicket(ticket: TicketRef, investigations: readonly InvestigationRef[] = []): string {
  return pathForIncident(ticket.incidentId, investigations);
}
