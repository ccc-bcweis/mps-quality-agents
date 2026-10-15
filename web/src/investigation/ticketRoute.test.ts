// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company
import { describe, expect, it } from "vitest";
import { S1_INCIDENT_ID, S1_INVESTIGATION_ID, S1_TICKET_ID, SUPPLIER_INCIDENT_ID, SUPPLIER_TICKET_ID } from "../mocks/demoData";
import { latestInvestigationForIncident, pathForTicket, type InvestigationRef } from "./ticketRoute";

const older: InvestigationRef = { id: "inv-old", incidentId: "incident-1" };
const newer: InvestigationRef = { id: "inv-new", incidentId: "incident-1" };
const other: InvestigationRef = { id: "inv-other", incidentId: "incident-2" };

describe("ticket investigation mapping", () => {
  it("uses the incident id and the latest investigation, never the ticket id or number", () => {
    const ticket = { id: "ticket-id", number: "TKT-1044", incidentId: "incident-1" };

    expect(latestInvestigationForIncident("incident-1", [older, other, newer])?.id).toBe("inv-new");
    expect(latestInvestigationForIncident("incident-1", [newer, older])?.id).toBe("inv-old");
    expect(latestInvestigationForIncident("missing", [older, newer])).toBeUndefined();

    const opened = pathForTicket(ticket, [older, other, newer]);
    expect(opened).toBe("/investigations/inv-new");
    expect(opened).not.toContain(ticket.id);
    expect(opened).not.toContain(ticket.number);

    const pending = pathForTicket(ticket, [other]);
    expect(pending).toBe("/investigations/incident/incident-1");
    expect(pending).not.toContain(ticket.id);
    expect(pending).not.toContain(ticket.number);
  });

  it("maps the demo tickets onto the seeded investigation or the incident start route", () => {
    const seeded = [{ id: S1_INVESTIGATION_ID, incidentId: S1_INCIDENT_ID }];
    expect(pathForTicket({ id: S1_TICKET_ID, number: "TKT-1044", incidentId: S1_INCIDENT_ID }, seeded)).toBe(
      `/investigations/${S1_INVESTIGATION_ID}`,
    );
    expect(pathForTicket({ id: SUPPLIER_TICKET_ID, number: "TKT-1038", incidentId: SUPPLIER_INCIDENT_ID })).toBe(
      `/investigations/incident/${SUPPLIER_INCIDENT_ID}`,
    );
  });
});
