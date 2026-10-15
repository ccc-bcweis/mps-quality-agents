// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company
import { describe, expect, it } from "vitest";
import type { Alert, Incident, Investigation, Ticket } from "../api/types";
import { linkedForTool } from "./linkedRecords";

describe("linkedForTool", () => {
  it("finds the tool's tickets, alerts and investigations and ignores other tools", () => {
    const tickets = [
      { id: "t1", number: "TKT-1001", incidentId: "i1", toolId: "E-01", title: "a", openedAt: "" },
      { id: "t2", number: "TKT-1002", incidentId: "i2", toolId: "E-02", title: "b", openedAt: "" },
    ] as Ticket[];
    const alerts = [
      { id: "a1", toolId: "E-01" },
      { id: "a2", toolId: "E-02" },
    ] as Alert[];
    const incidents = [
      { id: "i1", toolId: "E-01" },
      { id: "i2", toolId: "E-02" },
    ] as Incident[];
    const investigations = [
      { id: "v1", incidentId: "i1" },
      { id: "v2", incidentId: "i2" },
    ] as Investigation[];
    const linked = linkedForTool("E-01", { tickets, alerts, incidents, investigations });
    expect(linked.tickets.map((t) => t.number)).toEqual(["TKT-1001"]);
    expect(linked.alerts.map((a) => a.id)).toEqual(["a1"]);
    expect(linked.investigations.map((v) => v.id)).toEqual(["v1"]);
  });
});
