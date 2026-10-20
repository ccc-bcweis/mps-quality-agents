// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company
import { useEffect, useState } from "react";
import { useOutletContext } from "react-router-dom";
import { api, ApiProblem } from "../api/client";
import { formatEastern } from "../api/labels";
import type { JoinLinks } from "../api/types";
import type { ShellOutletContext } from "../components/AppShell";
import { btn, btnSm, field, panel } from "../components/ui";

function statusWord(status: string): string {
  if (status === "usedUp") return "Used up";
  if (status === "revoked") return "Revoked";
  if (status === "expired") return "Expired";
  if (status === "active") return "Active";
  return status;
}

async function copyText(value: string): Promise<string> {
  try {
    await navigator.clipboard.writeText(value);
    return "Link copied.";
  } catch {
    return value;
  }
}

export function MembersPage() {
  const { session } = useOutletContext<ShellOutletContext>();
  const [data, setData] = useState<JoinLinks | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [notice, setNotice] = useState<string | null>(null);
  const [pending, setPending] = useState(false);
  const [createHours, setCreateHours] = useState("72");
  const [createUses, setCreateUses] = useState("5");
  const [pasted, setPasted] = useState("");
  const [restoreHours, setRestoreHours] = useState("72");
  const [restoreUses, setRestoreUses] = useState("5");

  const admin = session?.isAdmin === true;

  useEffect(() => {
    if (!admin) return;
    let cancelled = false;
    api
      .listJoinLinks()
      .then((next) => {
        if (!cancelled) setData(next);
      })
      .catch((reason: unknown) => {
        if (!cancelled) setError(reason instanceof ApiProblem ? reason.title : "The member list did not load.");
      });
    return () => {
      cancelled = true;
    };
  }, [admin]);

  if (!session) {
    return (
      <div>
        <h1 className="text-mps-display font-semibold">Members</h1>
        <p className="mt-3 text-mps-muted">Loading members…</p>
      </div>
    );
  }

  if (!session.isAdmin) {
    return (
      <div>
        <h1 className="text-mps-display font-semibold">Members</h1>
        <p className="mt-3" role="alert">
          Only the admin can manage join links.
        </p>
      </div>
    );
  }

  async function reload() {
    const next = await api.listJoinLinks();
    setData(next);
  }

  async function run(action: () => Promise<string | null>) {
    setPending(true);
    setError(null);
    setNotice(null);
    try {
      const message = await action();
      if (message) setNotice(message);
      await reload();
    } catch (reason: unknown) {
      setError(reason instanceof ApiProblem ? reason.title : "That did not work.");
    } finally {
      setPending(false);
    }
  }

  const links = data?.links ?? [];
  const members = data?.members ?? [];

  return (
    <div>
      <h1 className="text-mps-display font-semibold">Members</h1>
      <p className="max-w-3xl text-mps-secondary">
        Create a join link, restore one you already sent, or revoke a person who should no longer be here. Times are Eastern Time (ET).
      </p>
      {error ? (
        <p className="mt-3 text-mps-alarm-text" role="alert">
          {error}
        </p>
      ) : null}
      {notice ? (
        <p className="mt-3" role="status">
          {notice}
        </p>
      ) : null}

      <section className={`${panel} mt-4`} aria-labelledby="create-link">
        <h2 id="create-link" className="text-mps-title font-semibold">
          Create a link
        </h2>
        <p className="text-mps-secondary">Expiry is from 1 minute to 30 days. A link allows 1 to 500 uses.</p>
        <div className="mt-3 grid gap-3 sm:grid-cols-2">
          <label className="block text-mps-body" htmlFor="create-hours">
            Expiry in hours
            <input
              id="create-hours"
              className={field}
              type="number"
              min={1 / 60}
              max={24 * 30}
              value={createHours}
              onChange={(event) => setCreateHours(event.target.value)}
            />
          </label>
          <label className="block text-mps-body" htmlFor="create-uses">
            Use limit
            <input
              id="create-uses"
              className={field}
              type="number"
              min={1}
              max={500}
              value={createUses}
              onChange={(event) => setCreateUses(event.target.value)}
            />
          </label>
        </div>
        <button
          type="button"
          className={`${btn} mt-3`}
          disabled={pending}
          onClick={() =>
            void run(async () => {
              const created = await api.createJoinLink(Number(createHours), Number(createUses));
              return copyText(created.url);
            })
          }
        >
          Create link
        </button>
      </section>

      <section className={`${panel} mt-4 overflow-x-auto`} aria-labelledby="link-list">
        <h2 id="link-list" className="mb-2 text-mps-title font-semibold">
          Links
        </h2>
        {!data && !error ? <p className="text-mps-muted">Loading members…</p> : null}
        {data && links.length === 0 ? <p className="text-mps-muted">No links yet.</p> : null}
        {links.length > 0 ? (
          <table className="w-full text-left">
            <thead>
              <tr className="text-mps-micro uppercase text-mps-muted">
                <th className="py-1 font-medium">Link</th>
                <th className="py-1 font-medium">Status</th>
                <th className="py-1 font-medium">Uses</th>
                <th className="py-1 font-medium">Expires</th>
                <th className="py-1 font-medium">Created</th>
                <th className="py-1 font-medium">
                  <span className="sr-only">Actions</span>
                </th>
              </tr>
            </thead>
            <tbody>
              {links.map((link) => (
                <tr key={link.id} className="border-t border-mps-line">
                  <td className="py-1.5 font-mono">{link.label}</td>
                  <td className="py-1.5">{statusWord(link.status)}</td>
                  <td className="py-1.5">
                    {link.uses} / {link.maxUses}
                  </td>
                  <td className="py-1.5 whitespace-nowrap">{formatEastern(link.expiresAt)}</td>
                  <td className="py-1.5 whitespace-nowrap">{formatEastern(link.createdAt)}</td>
                  <td className="py-1.5">
                    <div className="flex flex-wrap gap-2">
                      <button
                        type="button"
                        className={btnSm}
                        disabled={pending}
                        onClick={() => void run(async () => copyText(link.url))}
                      >
                        Copy link
                      </button>
                      {link.revoked ? (
                        <button
                          type="button"
                          className={btnSm}
                          disabled={pending}
                          aria-label={`Reactivate link ${link.label}`}
                          onClick={() => void run(async () => {
                            await api.reactivateJoinLink(link.id);
                            return "Link reactivated.";
                          })}
                        >
                          Reactivate
                        </button>
                      ) : (
                        <button
                          type="button"
                          className={btnSm}
                          disabled={pending}
                          aria-label={`Revoke link ${link.label}`}
                          onClick={() => void run(async () => {
                            await api.revokeJoinLink(link.id);
                            return "Link revoked.";
                          })}
                        >
                          Revoke
                        </button>
                      )}
                    </div>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        ) : null}
      </section>

      <section className={`${panel} mt-4`} aria-labelledby="restore-link">
        <h2 id="restore-link" className="text-mps-title font-semibold">
          Restore a link
        </h2>
        <p className="text-mps-secondary">Paste a join link or its token. The same token works again, with the expiry and use limit you set here.</p>
        <label className="mt-3 block text-mps-body" htmlFor="restore-token">
          Join link or token
          <input id="restore-token" className={field} value={pasted} onChange={(event) => setPasted(event.target.value)} />
        </label>
        <div className="mt-3 grid gap-3 sm:grid-cols-2">
          <label className="block text-mps-body" htmlFor="restore-hours">
            Restored expiry in hours
            <input
              id="restore-hours"
              className={field}
              type="number"
              min={1 / 60}
              max={24 * 30}
              value={restoreHours}
              onChange={(event) => setRestoreHours(event.target.value)}
            />
          </label>
          <label className="block text-mps-body" htmlFor="restore-uses">
            Restored use limit
            <input
              id="restore-uses"
              className={field}
              type="number"
              min={1}
              max={500}
              value={restoreUses}
              onChange={(event) => setRestoreUses(event.target.value)}
            />
          </label>
        </div>
        <button
          type="button"
          className={`${btn} mt-3`}
          disabled={pending}
          onClick={() =>
            void run(async () => {
              await api.restoreJoinLink(pasted.trim(), Number(restoreHours), Number(restoreUses));
              return "Link restored.";
            })
          }
        >
          Restore link
        </button>
      </section>

      <section className={`${panel} mt-4 overflow-x-auto`} aria-labelledby="member-list">
        <h2 id="member-list" className="mb-2 text-mps-title font-semibold">
          Members
        </h2>
        {data && members.length === 0 ? <p className="text-mps-muted">No members yet.</p> : null}
        {members.length > 0 ? (
          <table className="w-full text-left">
            <thead>
              <tr className="text-mps-micro uppercase text-mps-muted">
                <th className="py-1 font-medium">Name</th>
                <th className="py-1 font-medium">Email</th>
                <th className="py-1 font-medium">Joined</th>
                <th className="py-1 font-medium">Link</th>
                <th className="py-1 font-medium">Status</th>
                <th className="py-1 font-medium">
                  <span className="sr-only">Actions</span>
                </th>
              </tr>
            </thead>
            <tbody>
              {members.map((member) => {
                const name = member.displayName?.trim() || member.email?.trim() || "Member";
                return (
                  <tr key={member.memberKey} className="border-t border-mps-line">
                    <td className="py-1.5">{member.displayName?.trim() || "—"}</td>
                    <td className="py-1.5">{member.email?.trim() || "—"}</td>
                    <td className="py-1.5 whitespace-nowrap">{formatEastern(member.joinedAt)}</td>
                    <td className="py-1.5 font-mono">{member.linkLabel}</td>
                    <td className="py-1.5">{statusWord(member.status)}</td>
                    <td className="py-1.5">
                      {member.status === "active" ? (
                        <button
                          type="button"
                          className={btnSm}
                          disabled={pending}
                          aria-label={`Revoke ${name}`}
                          onClick={() => void run(async () => {
                            await api.revokeMember(member.memberKey);
                            return "Member revoked.";
                          })}
                        >
                          Revoke
                        </button>
                      ) : null}
                    </td>
                  </tr>
                );
              })}
            </tbody>
          </table>
        ) : null}
      </section>
    </div>
  );
}
