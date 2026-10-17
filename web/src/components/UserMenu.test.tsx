// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company
import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { readFileSync } from "node:fs";
import { resolve } from "node:path";
import { describe, expect, it, vi } from "vitest";
import { SIGNED_OUT_PAGE, signOutUrl } from "../auth/signOut";
import { UserMenu } from "./UserMenu";

describe("user menu", () => {
  it("opens from the signed-in name and signs out through the platform to the splash page", async () => {
    const user = userEvent.setup();
    const navigate = vi.fn();
    render(<UserMenu session={{ mode: "azure", isAdmin: false, displayName: "bcweis@commonconsulting.us" }} navigate={navigate} />);

    expect(screen.queryByRole("menu")).not.toBeInTheDocument();
    await user.click(screen.getByRole("button", { name: /Signed in bcweis@commonconsulting.us/ }));
    const signOut = screen.getByRole("menuitem", { name: "Sign out" });
    expect(signOut.className).toContain("bg-mps-alarm-solid");
    await user.click(signOut);

    expect(navigate).toHaveBeenCalledWith("/.auth/logout?post_logout_redirect_uri=%2Fsigned-out.html");
  });

  it("goes straight to the splash page when there is no platform sign-in", () => {
    expect(signOutUrl("local")).toBe(SIGNED_OUT_PAGE);
  });

  it("the splash page is branded and its Sign in button starts the organization sign-in", () => {
    const html = readFileSync(resolve(__dirname, "../../public/signed-out.html"), "utf8");
    expect(html).toContain("Meridian Precision Systems");
    expect(html).toContain("Fab Ops Console");
    expect(html).toMatch(/<a class="signin" href="\/\.auth\/login\/aad\?post_login_redirect_uri=\/"[^>]*>Sign in<\/a>/);
    expect(html).not.toMatch(/demo|mock/i);
  });
});
