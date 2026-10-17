// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company
import { useEffect, useId, useRef, useState } from "react";
import type { AdminSession } from "../api/types";
import { signOutUrl } from "../auth/signOut";
import { btnDanger } from "./ui";

/** The signed-in name opens a small menu with Sign out. */
export function UserMenu({ session, navigate = (url) => window.location.assign(url) }: { session: AdminSession | null; navigate?: (url: string) => void }) {
  const [open, setOpen] = useState(false);
  const menuId = useId();
  const root = useRef<HTMLSpanElement>(null);

  useEffect(() => {
    if (!open) return;
    function onPointer(event: MouseEvent) {
      if (root.current && !root.current.contains(event.target as Node)) setOpen(false);
    }
    document.addEventListener("mousedown", onPointer);
    return () => document.removeEventListener("mousedown", onPointer);
  }, [open]);

  return (
    <span ref={root} className="relative whitespace-nowrap">
      <button
        type="button"
        aria-haspopup="menu"
        aria-expanded={open}
        aria-controls={open ? menuId : undefined}
        className="rounded px-1 py-0.5 text-mps-muted hover:bg-mps-canvas focus-visible:outline focus-visible:outline-2 focus-visible:outline-mps-nominal"
        onClick={() => setOpen((value) => !value)}
        onKeyDown={(event) => {
          if (event.key === "Escape") setOpen(false);
        }}
      >
        <span>
          Signed in <b className="font-medium text-mps-secondary">{session?.displayName ?? "…"}</b> ▾
        </span>
      </button>
      {open ? (
        <div id={menuId} role="menu" aria-label="Account" className="absolute right-0 z-overlay mt-1 rounded border border-mps-line bg-mps-panel p-2 shadow-overlay">
          <button type="button" role="menuitem" className={btnDanger} onClick={() => navigate(signOutUrl(session?.mode))}>
            Sign out
          </button>
        </div>
      ) : null}
    </span>
  );
}
