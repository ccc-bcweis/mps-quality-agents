// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company
import { useEffect, useState } from "react";
import { Link, Outlet, useLocation, useOutletContext } from "react-router-dom";
import { api } from "../api/client";
import type { AdminSession } from "../api/types";
import { useConsole } from "../context/ConsoleContext";
import { CONSOLE_TABS } from "../shell/navigation";
import { Explain } from "./Explain";
import { GlossaryScope } from "./GlossaryScope";
import { HarnessBar } from "./HarnessBar";
import { Led } from "./Chip";
import { LifecycleBar } from "./LifecycleBar";
import { Term } from "./Term";

export type ShellOutletContext = {
  session: AdminSession | null;
};

function useAdminSession(): AdminSession | null {
  const [session, setSession] = useState<AdminSession | null>(null);
  useEffect(() => {
    let cancelled = false;
    api
      .getAdminSession()
      .then((next) => {
        if (!cancelled) setSession(next);
      })
      .catch(() => {
        if (!cancelled) setSession(null);
      });
    return () => {
      cancelled = true;
    };
  }, []);
  return session;
}

function NavLabel({ label }: { label: string }) {
  if (label === "CAPA") return <Term id="CAPA" spellOut={false} />;
  return label;
}

export function ConsoleFrame() {
  const location = useLocation();
  const { session } = useOutletContext<ShellOutletContext>();
  const { lifecycle } = useConsole();
  const [healthOn, setHealthOn] = useState(false);

  useEffect(() => {
    let cancelled = false;
    api
      .health()
      .then(() => {
        if (!cancelled) setHealthOn(true);
      })
      .catch(() => {
        if (!cancelled) setHealthOn(false);
      });
    return () => {
      cancelled = true;
    };
  }, []);

  return (
    <section
      aria-label="Meridian Precision Systems console"
      data-region="console"
      className="flex flex-1 flex-col rounded border-2 border-sn-gold bg-mps-canvas font-sans text-mps-text"
    >
      <header className="flex flex-wrap items-center gap-4 border-b border-mps-line bg-mps-panel px-4 py-2">
        <Link to="/" className="flex items-center gap-2.5 text-mps-text no-underline hover:no-underline">
          <img src="/logo-concept-1.svg" width={32} height={32} alt="Meridian Precision Systems mark" />
          <span>
            <strong className="block text-mps-body">Meridian Precision Systems</strong>
            <span className="block font-mono text-mps-micro uppercase tracking-wider text-mps-muted">Fab Ops Console · Austin Demo Fab</span>
          </span>
        </Link>
        <div className="ml-auto flex flex-wrap items-center gap-4 font-mono text-mps-micro text-mps-muted">
          <span className="whitespace-nowrap">
            <Led tone={healthOn ? "nominal" : "off"} /> Sim clock · 1 s = 1 min
          </span>
          <span className="whitespace-nowrap">
            Plant <b className="font-medium text-mps-secondary">AUS-1</b>
          </span>
          <span className="whitespace-nowrap">
            Engineer <b className="font-medium text-mps-secondary">{session?.displayName ?? "dev-user"}</b>
          </span>
          <Link
            to="/glossary"
            aria-current={location.pathname.startsWith("/glossary") ? "page" : undefined}
            className="whitespace-nowrap text-mps-nominal"
          >
            Glossary
          </Link>
        </div>
      </header>
      <nav
        aria-label="Console"
        data-sites-slot="before-fleet"
        className="flex flex-wrap gap-1 border-b border-mps-line bg-mps-panel px-3"
      >
        {CONSOLE_TABS.map((item) => {
          const active = item.match(location.pathname);
          return (
            <Link
              key={item.label}
              to={item.to}
              aria-current={active ? "page" : undefined}
              className={`shrink-0 whitespace-nowrap border-b-2 px-3 py-2 text-mps-body no-underline ${active ? "border-mps-nominal text-mps-nominal" : "border-transparent text-mps-muted hover:text-mps-text"}`}
            >
              <NavLabel label={item.label} />
            </Link>
          );
        })}
      </nav>
      <LifecycleBar current={lifecycle.current} escalated={lifecycle.escalated} />
      <main
        id="main"
        className={`mx-auto w-full flex-1 px-4 py-4 ${location.pathname.startsWith("/tools/") ? "max-w-[1680px]" : "max-w-[1200px]"}`}
      >
        <Outlet />
      </main>
      <footer className="mt-auto flex flex-wrap items-center justify-between gap-3 border-t border-mps-line px-4 py-3 text-mps-micro text-mps-muted">
        <span>
          Copyright (c) 2026 Bryan C. Weis and Common Consulting Company · <Explain text="MIT" /> · Fictional demonstration. All companies and data are synthetic.
        </span>
        <a className="whitespace-nowrap text-mps-nominal" href="https://github.com/ccc-bcweis/mps-quality-agents">
          How we built it
        </a>
      </footer>
    </section>
  );
}

export function HarnessFrame() {
  return (
    <section
      aria-label="Demo harness panel"
      data-region="harness-panel"
      className="flex flex-1 flex-col rounded border-2 border-dashed border-sn-gold bg-[#07111f] font-sans text-mps-text"
    >
      <p className="border-b border-sn-gold/40 px-4 py-2 font-pitch text-mps-body text-sn-gold">
        Presenter tool. This is not a Meridian Precision Systems console screen.{" "}
        <Link className="whitespace-nowrap text-sn-gold-wash underline" to="/">
          Open the console
        </Link>
      </p>
      <main id="main" className="mx-auto w-full max-w-[1200px] flex-1 px-4 py-4">
        <Outlet />
      </main>
      <footer className="mt-auto px-4 py-3 font-sans text-mps-micro text-sn-gold-wash">
        Copyright (c) 2026 Bryan C. Weis and Common Consulting Company · <Explain text="MIT" /> · Fictional demonstration. All companies and data are synthetic.
      </footer>
    </section>
  );
}

export function AppShell() {
  const location = useLocation();
  const session = useAdminSession();
  const outlet: ShellOutletContext = { session };
  return (
    <GlossaryScope key={location.pathname}>
      <div className="flex min-h-screen flex-col bg-sn-navy">
        <a className="sr-only focus:not-sr-only focus:absolute focus:left-2 focus:top-2 focus:z-toast focus:bg-mps-panel focus:p-2" href="#main">
          Skip to content
        </a>
        <HarnessBar session={session} />
        <div className="flex flex-1 flex-col px-2 pb-2 pt-2">
          <Outlet context={outlet} />
        </div>
      </div>
    </GlossaryScope>
  );
}
