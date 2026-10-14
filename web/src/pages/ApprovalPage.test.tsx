// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company
import { screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { describe, expect, it } from "vitest";
import { S1_INVESTIGATION_ID } from "../mocks/demoData";
import { renderConsole } from "../test/render";

describe("approval dialog", () => {
  it("does not issue a token until APPROVE is typed and confirmed", async () => {
    const user = userEvent.setup();
    renderConsole(`/approvals/${S1_INVESTIGATION_ID}`);
    await user.click(await screen.findByRole("button", { name: "Approve CAPA + containment" }));
    const issue = await screen.findByRole("button", { name: "Issue token and create CAPA" });
    expect(issue).toBeDisabled();
    await user.type(screen.getByLabelText("Type APPROVE to confirm"), "approve");
    expect(issue).toBeDisabled();
    await user.clear(screen.getByLabelText("Type APPROVE to confirm"));
    await user.type(screen.getByLabelText("Type APPROVE to confirm"), "APPROVE");
    expect(issue).toBeEnabled();
    await user.click(screen.getByRole("button", { name: "Cancel" }));
    expect(screen.queryByRole("dialog")).not.toBeInTheDocument();
    expect(screen.queryByText("77777777-7777-4777-8777-777777777777")).not.toBeInTheDocument();

    await user.click(screen.getByRole("button", { name: "Approve CAPA + containment" }));
    await user.type(await screen.findByLabelText("Type APPROVE to confirm"), "APPROVE");
    await user.click(screen.getByRole("button", { name: "Issue token and create CAPA" }));
    expect(await screen.findByText("77777777-7777-4777-8777-777777777777")).toBeInTheDocument();
    expect(screen.getByText(/Hold the affected lots and confirm the edge ring revision/)).toBeInTheDocument();
  });

  it("rejects without creating a CAPA", async () => {
    const user = userEvent.setup();
    renderConsole(`/approvals/${S1_INVESTIGATION_ID}`);
    await user.click(await screen.findByRole("button", { name: "Reject" }));
    const reject = await screen.findByRole("button", { name: "Reject recommendation" });
    expect(reject).toBeDisabled();
    await user.type(screen.getByLabelText("Reject reason"), "The confounder is not addressed.");
    await user.click(reject);
    expect(await screen.findByText(/Stage rejected/)).toBeInTheDocument();
    expect(screen.queryByText("77777777-7777-4777-8777-777777777777")).not.toBeInTheDocument();
  });

  it("surfaces the server error when advance is illegal", async () => {
    const user = userEvent.setup();
    renderConsole(`/approvals/${S1_INVESTIGATION_ID}`);
    await user.click(await screen.findByRole("button", { name: "Approve CAPA + containment" }));
    await user.type(await screen.findByLabelText("Type APPROVE to confirm"), "APPROVE");
    await user.click(screen.getByRole("button", { name: "Issue token and create CAPA" }));
    await screen.findByText("77777777-7777-4777-8777-777777777777");
    await user.click(screen.getByRole("button", { name: "Advance stage" }));
    expect(await screen.findByRole("alert")).toHaveTextContent("Illegal CAPA transition.");
  });
});
