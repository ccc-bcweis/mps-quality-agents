// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company
import { screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { http, HttpResponse } from "msw";
import { describe, expect, it } from "vitest";
import { metadataSettings } from "../mocks/handlers";
import { NOT_SAVED_NOTICE } from "../rules/RuleEditor";
import { renderConsole } from "../test/render";
import { server } from "../test/server";

describe("rules and process", () => {
  it("shows the rules in plain language with their settings as fields read from the data", async () => {
    renderConsole("/rules");

    expect(await screen.findByRole("heading", { name: "Rules & Process" })).toBeInTheDocument();
    expect(await screen.findByText("When the severity is critical.")).toBeInTheDocument();
    expect(screen.getByText(/Open a ticket, set the priority to critical/)).toBeInTheDocument();
    expect(screen.getByText(/A critical signal can affect the wafers or the tool/)).toBeInTheDocument();
    expect(screen.queryByText("rule-critical-human")).not.toBeInTheDocument();
    expect(screen.getByText("When 8 readings in a row stay inside two sigma after an alert.")).toBeInTheDocument();
    expect(screen.getByText(/Close the incident and its ticket\. The investigation keeps its Approved result\./)).toBeInTheDocument();

    const control = screen.getByRole("spinbutton", { name: "Control limits" });
    expect(control).toHaveValue(3);
    expect(control).toBeDisabled();
    expect(screen.getByRole("textbox", { name: "Critical signal needs a person: Severity is" })).toHaveValue("critical");
    expect(screen.getByRole("textbox", { name: "Critical signal needs a person: Queue" })).toHaveValue("Shift lead");
    expect(within(screen.getByRole("row", { name: /When the severity is critical/ })).getByRole("switch", { name: "Ask a person to review" })).toBeChecked();
    expect(screen.getByRole("switch", { name: "Rule 4 on" })).toBeChecked();
  });

  it("takes its values from the server, not from the page", async () => {
    server.use(
      http.get("/api/metadata/settings", () =>
        HttpResponse.json(metadataSettings.map((setting) => (setting.id === "spc.controlLimitSigma" ? { ...setting, value: "2.5" } : setting))),
      ),
    );
    renderConsole("/rules");
    expect(await screen.findByRole("spinbutton", { name: "Control limits" })).toHaveValue(2.5);
    expect(await screen.findByRole("list", { name: "Quality flow" })).toHaveTextContent(/control limits at 2\.5 sigma/);
  });

  it("lets a person edit the fields, and Save says plainly the change is not kept", async () => {
    const user = userEvent.setup();
    renderConsole("/rules");
    await user.click(await screen.findByRole("button", { name: "Edit" }));
    const control = screen.getByRole("spinbutton", { name: "Control limits" });
    expect(control).toBeEnabled();
    await user.clear(control);
    await user.type(control, "4");
    await user.click(screen.getByRole("switch", { name: "Rule 4 on" }));
    expect(control).toHaveValue(4);
    await user.click(screen.getByRole("button", { name: "Save" }));
    expect(screen.getByTestId("rules-notice")).toHaveTextContent(NOT_SAVED_NOTICE);
    expect(screen.getByTestId("rules-notice").textContent).not.toMatch(/demo/i);
    expect(screen.getByRole("spinbutton", { name: "Control limits" })).toHaveValue(3);
    expect(screen.getByRole("spinbutton", { name: "Control limits" })).toBeDisabled();
    expect(screen.getByRole("switch", { name: "Rule 4 on" })).toBeChecked();
  });

  it("draws one map of the actual flow, from readings to close, without the old generic boxes", async () => {
    renderConsole("/rules");
    const map = await screen.findByRole("list", { name: "Quality flow" });
    const stages = within(map)
      .getAllByRole("listitem")
      .map((item) => item.getAttribute("data-stage"))
      .filter(Boolean);
    expect(stages).toEqual(["readings", "rules", "alert", "ticket", "investigation", "decision", "capa", "containment", "close"]);
    const choices = within(map).getByRole("list", { name: "Decision choices" });
    expect(within(choices).getAllByRole("listitem").map((item) => item.textContent?.split(" → ")[0])).toEqual([
      "Approve CAPA",
      "Request more evidence",
      "Override with justification",
      "Reject",
    ]);
    expect(map).toHaveTextContent(/Western Electric rule 4: 8 readings in a row on one side/);
    expect(map).toHaveTextContent(/2 rules open a ticket/);
    expect(map).toHaveTextContent(/alert clears after 8 readings in a row/);
    expect(screen.queryByRole("button", { name: "Current state" })).not.toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Future state" })).not.toBeInTheDocument();
    expect(screen.queryByText("Someone notices the excursion")).not.toBeInTheDocument();
    expect(screen.queryByText("Hand off by email")).not.toBeInTheDocument();
    expect(screen.queryByRole("heading", { name: "Moves" })).not.toBeInTheDocument();
    expect(screen.queryByRole("group", { name: "Tickets" })).not.toBeInTheDocument();
  });
});
