// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company
import { screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { http, HttpResponse } from "msw";
import { describe, expect, it, vi } from "vitest";
import { formatEastern } from "../api/labels";
import { server } from "../test/server";
import { expandHarness } from "../test/harness";
import { renderConsole } from "../test/render";

const TOKEN = "ab".repeat(24);
const ACTIVE_ID = "abcdef12-3456-7890-abcd-ef1234567890";
const REVOKED_ID = "bbbbbb12-3456-7890-abcd-ef1234567890";
const MEMBER_KEY = "9188040d-6c67-4c5b-b112-36a304b66dad:oid-guest";
const JOINED = "2026-01-15T18:30:00.000Z";

const fixture = {
  links: [
    {
      id: ACTIVE_ID,
      url: `https://example.test/join/${TOKEN}`,
      createdAt: JOINED,
      expiresAt: "2026-01-16T18:30:00.000Z",
      maxUses: 5,
      uses: 1,
      revoked: false,
      status: "active",
      label: "abcdef",
    },
    {
      id: REVOKED_ID,
      url: "https://example.test/join/" + "cd".repeat(24),
      createdAt: JOINED,
      expiresAt: "2026-01-16T18:30:00.000Z",
      maxUses: 2,
      uses: 2,
      revoked: true,
      status: "revoked",
      label: "bbbbbb",
    },
  ],
  members: [
    {
      email: "guest@outlook.com",
      displayName: "Guest One",
      joinedAt: JOINED,
      linkLabel: "abcdef",
      status: "active",
      memberKey: MEMBER_KEY,
      tenantId: "9188040d-6c67-4c5b-b112-36a304b66dad",
      objectId: "oid-guest",
      revokedAt: null,
    },
  ],
};

describe("eastern time", () => {
  it("formats January and July in New York time", () => {
    expect(formatEastern("2026-01-15T18:30:00.000Z")).toBe("Jan 15, 2026, 13:30 ET");
    expect(formatEastern("2026-07-15T18:30:00.000Z")).toBe("Jul 15, 2026, 14:30 ET");
  });
});

describe("members page", () => {
  it("shows links and members, and revoke and restore call the admin routes", async () => {
    const calls: { method: string; path: string; body?: unknown }[] = [];
    server.use(
      http.get("/api/admin/session", () =>
        HttpResponse.json({ mode: "local", isAdmin: true, displayName: "Owner", member: true }),
      ),
      http.get("/api/admin/join-links", () => HttpResponse.json(fixture)),
      http.post("/api/admin/join-links", async ({ request }) => {
        calls.push({ method: "POST", path: new URL(request.url).pathname, body: await request.json() });
        return HttpResponse.json(fixture.links[0]);
      }),
      http.post("/api/admin/join-links/restore", async ({ request }) => {
        calls.push({ method: "POST", path: new URL(request.url).pathname, body: await request.json() });
        return HttpResponse.json(fixture.links[0]);
      }),
      http.post("/api/admin/join-links/:id/reactivate", ({ request }) => {
        calls.push({ method: "POST", path: new URL(request.url).pathname });
        return new HttpResponse(null, { status: 204 });
      }),
      http.delete("/api/admin/join-links/:id", ({ request }) => {
        calls.push({ method: "DELETE", path: new URL(request.url).pathname });
        return new HttpResponse(null, { status: 204 });
      }),
      http.post("/api/admin/members/revoke", async ({ request }) => {
        calls.push({ method: "POST", path: new URL(request.url).pathname, body: await request.json() });
        return new HttpResponse(null, { status: 204 });
      }),
    );
    const user = userEvent.setup();
    const writeText = vi.fn().mockResolvedValue(undefined);
    Object.defineProperty(navigator, "clipboard", { configurable: true, value: { writeText } });
    renderConsole("/admin/members");
    await screen.findAllByText("Jan 15, 2026, 13:30 ET");
    const heading = screen.getByRole("heading", { level: 1, name: "Members" });
    const page = heading.closest("div");
    expect(page).not.toBeNull();
    const text = page?.textContent ?? "";
    expect(text).toContain("Jan 15, 2026, 13:30 ET");
    expect(text).toContain("abcdef");
    expect(text).toContain("Guest One");
    expect(text).toContain("guest@outlook.com");
    expect(text).not.toContain(ACTIVE_ID);
    expect(text).not.toContain(TOKEN);
    expect(text).not.toContain(MEMBER_KEY);
    expect(text).not.toMatch(/demo|mock/i);

    await user.click(screen.getByRole("button", { name: "Revoke link abcdef" }));
    await screen.findByText("Link revoked.");
    expect(calls.some((call) => call.method === "DELETE" && call.path === `/api/admin/join-links/${ACTIVE_ID}`)).toBe(true);

    await user.click(screen.getByRole("button", { name: "Reactivate link bbbbbb" }));
    await screen.findByText("Link reactivated.");
    expect(calls.some((call) => call.method === "POST" && call.path === `/api/admin/join-links/${REVOKED_ID}/reactivate`)).toBe(true);

    await user.clear(screen.getByLabelText("Join link or token"));
    await user.type(screen.getByLabelText("Join link or token"), `https://example.test/join/${TOKEN}`);
    await user.clear(screen.getByLabelText("Restored expiry in hours"));
    await user.type(screen.getByLabelText("Restored expiry in hours"), "2");
    await user.clear(screen.getByLabelText("Restored use limit"));
    await user.type(screen.getByLabelText("Restored use limit"), "3");
    await user.click(screen.getByRole("button", { name: "Restore link" }));
    await screen.findByText("Link restored.");
    expect(calls).toContainEqual({
      method: "POST",
      path: "/api/admin/join-links/restore",
      body: { token: `https://example.test/join/${TOKEN}`, hours: 2, maxUses: 3 },
    });

    await user.click(screen.getByRole("button", { name: "Create link" }));
    await screen.findByText("Link copied.");
    expect(writeText).toHaveBeenCalledWith(fixture.links[0].url);
    expect(calls).toContainEqual({
      method: "POST",
      path: "/api/admin/join-links",
      body: { hours: 72, maxUses: 5 },
    });

    await user.click(screen.getByRole("button", { name: "Revoke Guest One" }));
    await screen.findByText("Member revoked.");
    expect(calls).toContainEqual({
      method: "POST",
      path: "/api/admin/members/revoke",
      body: { memberKey: MEMBER_KEY },
    });

    const harness = await expandHarness();
    const join = within(harness).getByRole("link", { name: "Join links" });
    expect(join).toHaveAttribute("href", "/admin/members");
  });

  it("tells a non-admin that only the admin can manage join links", async () => {
    let listed = false;
    server.use(
      http.get("/api/admin/session", () =>
        HttpResponse.json({ mode: "local", isAdmin: false, displayName: "Colleague", member: true }),
      ),
      http.get("/api/admin/join-links", () => {
        listed = true;
        return HttpResponse.json(fixture);
      }),
    );
    renderConsole("/admin/members");
    expect(await screen.findByRole("alert")).toHaveTextContent("Only the admin can manage join links.");
    expect(screen.queryByRole("table")).not.toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Create link" })).not.toBeInTheDocument();
    expect(listed).toBe(false);
    const harness = await expandHarness();
    expect(within(harness).queryByRole("link", { name: "Join links" })).not.toBeInTheDocument();
  });
});
