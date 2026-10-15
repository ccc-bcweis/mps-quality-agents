// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company
import { screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { http, HttpResponse } from "msw";
import { describe, expect, it } from "vitest";
import { approvalSentence } from "../api/labels";
import { server } from "../test/server";
import { expandHarness } from "../test/harness";
import { renderConsole } from "../test/render";

const AT = "2026-10-15T02:42:00.000Z";

describe("audit log", () => {
  it("opens from the harness and starts empty", async () => {
    const user = userEvent.setup();
    renderConsole("/");
    const harness = await expandHarness();
    await user.click(within(harness).getByRole("link", { name: "Audit log" }));
    const panel = await screen.findByRole("region", { name: "Demo harness panel" });
    expect(within(panel).getByRole("heading", { name: "Audit log" })).toBeInTheDocument();
    expect(within(panel).getByText(/No audit events yet/)).toBeInTheDocument();
    expect(screen.queryByRole("region", { name: "Meridian Precision Systems console" })).not.toBeInTheDocument();
  });

  it("shows the trail and the correlation trace", async () => {
    server.use(
      http.get("/api/audit", () =>
        HttpResponse.json({
          entries: [
            {
              at: AT,
              kind: "approval",
              summary: "Approved by Bryan Weis.",
              actor: "Bryan Weis",
              toolId: "E-02",
              correlationId: "corr-audit-1",
              recordId: "apr-1",
            },
          ],
          traces: [
            {
              correlationId: "corr-audit-1",
              events: [
                {
                  at: AT,
                  kind: "approval",
                  summary: "Approved by Bryan Weis.",
                  actor: "Bryan Weis",
                  toolId: "E-02",
                  correlationId: "corr-audit-1",
                  recordId: "apr-1",
                },
              ],
            },
          ],
        }),
      ),
    );
    renderConsole("/harness/audit");
    const panel = await screen.findByRole("region", { name: "Demo harness panel" });
    expect(await within(panel).findAllByText("Approved by Bryan Weis.")).toHaveLength(2);
    expect(within(panel).getByRole("heading", { name: "Correlation traces" })).toBeInTheDocument();
    expect(within(panel).getByText("corr-audit-1")).toBeInTheDocument();
    expect(approvalSentence("Bryan Weis", AT)).toMatch(/Approved by Bryan Weis at \d{1,2}:\d{2} [AP]M/);
  });
});
