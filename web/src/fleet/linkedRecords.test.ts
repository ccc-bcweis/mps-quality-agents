// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company
import { describe, expect, it } from "vitest";
import type { Alert, Incident, Investigation, ManualReading, Ticket } from "../api/types";
import { orderedLinked } from "./linkedRecords";

const incident = (id: string, openedAt: string) => ({ id, toolId: "E-01", openedAt }) as unknown as Incident;
const ticket = (id: string, incidentId: string, openedAt: string) => ({ id, incidentId, toolId: "E-01", number: id, title: id, openedAt }) as unknown as Ticket;
const alert = (id: string, raisedAt: string) => ({ id, toolId: "E-01", raisedAt, rule: "western-electric-1" }) as unknown as Alert;
const investigation = (id: string, incidentId: string) => ({ id, incidentId, stage: "started" }) as unknown as Investigation;

describe("linked records order", () => {
  it("lists every record newest first, and at one moment the investigation before its ticket before its alert", () => {
    const incidents = [incident("n1", "2026-10-17T06:00:00Z"), incident("n2", "2026-10-17T07:00:00Z")];
    const linked = {
      tickets: [ticket("t1", "n1", "2026-10-17T06:00:00Z"), ticket("t2", "n2", "2026-10-17T07:00:00Z")],
      alerts: [alert("a1", "2026-10-17T06:00:00Z"), alert("a2", "2026-10-17T07:00:00Z")],
      investigations: [investigation("i1", "n1"), investigation("i2", "n2")],
    };
    const manual = [{ evidenceId: "m1", observedAt: "2026-10-17T06:30:00Z" } as unknown as ManualReading];

    const order = orderedLinked(linked, incidents, manual, 20).map((entry) => entry.id);

    expect(order).toEqual(["i2", "t2", "a2", "m1", "i1", "t1", "a1"]);
    expect(orderedLinked(linked, incidents, manual, 3).map((entry) => entry.id)).toEqual(["i2", "t2", "a2"]);
  });
});
