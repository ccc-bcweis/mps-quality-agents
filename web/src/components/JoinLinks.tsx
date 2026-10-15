// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company
import { useCallback, useEffect, useState } from "react";
import { api, ApiProblem } from "../api/client";
import type { JoinLink, JoinLinks as JoinLinksData } from "../api/types";
import { formatStamp } from "../api/labels";

const smallBtn =
  "inline-flex shrink-0 items-center whitespace-nowrap rounded border border-sn-gold px-2 py-0.5 text-mps-label text-sn-gold hover:bg-sn-navy-soft disabled:opacity-40";

const STATUS: Record<string, string> = { active: "Active", expired: "Expired", usedUp: "Used up", revoked: "Revoked" };

/** Admin-only dialog: make a join link with an expiry and a use limit, copy it, list links and members, revoke a link. */
export function JoinLinksDialog({ onClose }: { onClose: () => void }) {
  const [data, setData] = useState<JoinLinksData | null>(null);
  const [hours, setHours] = useState(72);
  const [uses, setUses] = useState(5);
  const [message, setMessage] = useState<string | null>(null);
  const [pending, setPending] = useState(false);

  const load = useCallback(() => {
    api
      .listJoinLinks()
      .then(setData)
      .catch((reason: unknown) => setMessage(reason instanceof ApiProblem ? reason.title : "Join links did not load."));
  }, []);

  useEffect(() => {
    load();
  }, [load]);

  async function create() {
    setPending(true);
    setMessage(null);
    try {
      const link = await api.createJoinLink(hours, uses);
      await copy(link);
      load();
    } catch (reason) {
      setMessage(reason instanceof ApiProblem ? reason.title : "The link was not created.");
    } finally {
      setPending(false);
    }
  }

  async function copy(link: JoinLink) {
    try {
      await navigator.clipboard.writeText(link.url);
      setMessage("Link copied.");
    } catch {
      setMessage(link.url);
    }
  }

  async function revoke(link: JoinLink) {
    await api.revokeJoinLink(link.id).catch(() => undefined);
    load();
  }

  return (
    <div role="dialog" aria-modal="true" aria-label="Join links" className="fixed inset-0 z-toast flex items-start justify-center bg-black/50 p-6">
      <div className="max-h-[80vh] w-full max-w-3xl overflow-y-auto rounded border border-sn-gold bg-sn-navy p-4 text-sn-gold-wash">
        <div className="mb-3 flex items-center justify-between">
          <h2 className="text-mps-title font-semibold text-sn-gold">Join links</h2>
          <button type="button" className={smallBtn} onClick={onClose}>
            Close
          </button>
        </div>
        <p className="mb-3 text-mps-body">Anyone who opens an active link and signs in with a Microsoft account joins the demo with full rights.</p>
        <div className="mb-3 flex flex-wrap items-end gap-3">
          <label className="text-mps-label">
            Expires in (hours)
            <input aria-label="Expires in hours" type="number" min={1} max={720} value={hours} onChange={(event) => setHours(Number(event.target.value))} className="ml-2 w-20 rounded border border-sn-gold/50 bg-transparent px-1" />
          </label>
          <label className="text-mps-label">
            Max uses
            <input aria-label="Max uses" type="number" min={1} max={500} value={uses} onChange={(event) => setUses(Number(event.target.value))} className="ml-2 w-16 rounded border border-sn-gold/50 bg-transparent px-1" />
          </label>
          <button type="button" className={smallBtn} disabled={pending} onClick={() => void create()}>
            Create and copy link
          </button>
        </div>
        {message ? <p role="status" className="mb-3 break-all text-mps-body">{message}</p> : null}
        <table aria-label="Join links list" className="w-full text-left text-mps-label">
          <thead>
            <tr>
              <th className="py-1">Created</th>
              <th className="py-1">Expires</th>
              <th className="py-1">Uses</th>
              <th className="py-1">Status</th>
              <th className="py-1">Actions</th>
            </tr>
          </thead>
          <tbody>
            {(data?.links ?? []).map((link) => (
              <tr key={link.id} className="border-t border-sn-gold/30">
                <td className="py-1">{formatStamp(link.createdAt)}</td>
                <td className="py-1">{formatStamp(link.expiresAt)}</td>
                <td className="py-1">
                  {link.uses} / {link.maxUses}
                </td>
                <td className="py-1">{STATUS[link.status] ?? link.status}</td>
                <td className="flex gap-1 py-1">
                  <button type="button" className={smallBtn} onClick={() => void copy(link)}>
                    Copy
                  </button>
                  <button type="button" className={smallBtn} disabled={link.status !== "active"} onClick={() => void revoke(link)}>
                    Revoke
                  </button>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
        {data && data.links.length === 0 ? <p className="mt-2">No links yet.</p> : null}
        <h3 className="mt-4 font-semibold text-sn-gold">Joined users</h3>
        {data && data.members.length > 0 ? (
          <ul className="mt-1 text-mps-label">
            {data.members.map((member) => (
              <li key={`${member.email}-${member.joinedAt}`}>
                {member.displayName ?? member.email} · {member.email} · {formatStamp(member.joinedAt)}
              </li>
            ))}
          </ul>
        ) : (
          <p className="mt-1 text-mps-label">Nobody has joined through a link yet.</p>
        )}
      </div>
    </div>
  );
}
