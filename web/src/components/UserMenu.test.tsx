// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company
import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { readFileSync } from "node:fs";
import { resolve } from "node:path";
import { describe, expect, it, vi } from "vitest";
import { LOG_IN_URL, NO_ACCESS_PAGE, SIGNED_OUT_PAGE, entryRedirect, landingFor, signOutUrl } from "../auth/signOut";
import { UserMenu } from "./UserMenu";

describe("user menu", () => {
  it("opens from the signed-in name and signs out through the platform to the landing page", async () => {
    const user = userEvent.setup();
    const navigate = vi.fn();
    render(<UserMenu session={{ mode: "azure", isAdmin: false, displayName: "bcweis@commonconsulting.us" }} navigate={navigate} />);

    expect(screen.queryByRole("menu")).not.toBeInTheDocument();
    await user.click(screen.getByRole("button", { name: /Signed in bcweis@commonconsulting.us/ }));
    const signOut = screen.getByRole("menuitem", { name: "Sign out" });
    expect(signOut.className).toContain("bg-mps-alarm-solid");
    await user.click(signOut);

    expect(navigate).toHaveBeenCalledWith("/.auth/logout?post_logout_redirect_uri=%2Flanding.html%3FsignedOut%3D1");
  });

  it("goes straight to the landing page when there is no platform sign-in", () => {
    expect(signOutUrl("local")).toBe(SIGNED_OUT_PAGE);
  });

  it("the landing page is branded and its Log in button opens the Entra account picker", () => {
    const html = readFileSync(resolve(__dirname, "../../public/landing.html"), "utf8");
    expect(LOG_IN_URL).toBe("/.auth/login/aad?prompt=select_account&post_login_redirect_uri=/");
    expect(html).toContain("Meridian Precision Systems");
    expect(html).toContain("Fab Ops Console");
    expect(html).toMatch(/<a class="login" id="landing-action" href="\/\.auth\/login\/aad\?prompt=select_account&amp;post_login_redirect_uri=\/"[^>]*>Log in<\/a>/);
    expect(html).toContain("which has no access");
    expect(html).toContain("Use a different account");
    expect(html).not.toMatch(/Sign in|demo|mock/i);
  });

  it("sends a signed-out console to the landing page and an account without access to the no-access page", () => {
    expect(entryRedirect({ status: 401 }, "/")).toBe("/landing.html");
    expect(entryRedirect({ status: 401 }, "/tools/E-02")).toBe(landingFor("/tools/E-02"));
    expect(landingFor("/tools/E-02")).toBe("/landing.html?next=%2Ftools%2FE-02");
    expect(entryRedirect({ status: 403 }, "/")).toBe(NO_ACCESS_PAGE);
    expect(entryRedirect({ session: { mode: "azure", isAdmin: false, displayName: "guest@example.com", member: false } }, "/")).toBe(NO_ACCESS_PAGE);
    expect(entryRedirect({ session: { mode: "azure", isAdmin: false, displayName: "bcweis@commonconsulting.us", member: true } }, "/")).toBeNull();
    expect(entryRedirect({ status: 500 }, "/")).toBeNull();
  });
});
