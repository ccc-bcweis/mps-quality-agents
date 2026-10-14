// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company
import { screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { describe, expect, it } from "vitest";
import { renderConsole } from "../test/render";

describe("glossary page", () => {
  it("lists a specification term and filters the table", async () => {
    const user = userEvent.setup();
    renderConsole("/glossary");
    expect(await screen.findByRole("heading", { name: "Glossary" })).toBeInTheDocument();
    expect(screen.getByText(/statistical process control/i)).toBeInTheDocument();
    expect(screen.getByText(/overall equipment effectiveness/i)).toBeInTheDocument();
    expect(screen.getByText("mTorr")).toBeInTheDocument();
    await user.type(screen.getByLabelText("Filter glossary"), "chuck");
    expect(screen.getByText(/electrostatic chuck/i)).toBeInTheDocument();
    expect(screen.queryByText(/overall equipment effectiveness/i)).not.toBeInTheDocument();
  });
});
