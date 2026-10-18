// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company
import { screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { http, HttpResponse } from "msw";
import { describe, expect, it } from "vitest";
import { investigations, S2_INVESTIGATION_ID } from "../mocks/demoData";
import { renderConsole } from "../test/render";
import { server } from "../test/server";

describe("escalation detail", () => {
  it("shows what the AI proposed when a check refused its answer", async () => {
    const base = investigations.find((item) => item.id === S2_INVESTIGATION_ID)!;
    const cited = base.evidence[0];
    server.use(
      http.get(`/api/investigations/${S2_INVESTIGATION_ID}`, () =>
        HttpResponse.json({
          ...base,
          escalationReason: "The AI named SN-SIH4-088 as the cause, but that record says in its own words it is not the cause. A person should review it.",
          rejectedAnswer: {
            cause: "An operator edited the RF power recipe setpoint on E-03 with no engineering change order.",
            category: "processDrift",
            evidenceIds: [cited.id],
          },
        }),
      ),
    );

    renderConsole(`/investigations/${S2_INVESTIGATION_ID}`);
    const detail = await screen.findByText("What the AI proposed (not accepted)");
    await userEvent.click(detail);
    const box = detail.closest("details")!;
    expect(within(box as HTMLElement).getByText(/recipe setpoint on E-03/)).toBeInTheDocument();
    expect(box).toHaveTextContent(/process drift/i);
    expect(box).toHaveTextContent(cited.title);
  });

  it("shows no proposal when the round was not refused by a check", async () => {
    renderConsole(`/investigations/${S2_INVESTIGATION_ID}`);
    expect((await screen.findAllByText(/No root cause is asserted/)).length).toBeGreaterThan(0);
    expect(screen.queryByText("What the AI proposed (not accepted)")).not.toBeInTheDocument();
  });
});
