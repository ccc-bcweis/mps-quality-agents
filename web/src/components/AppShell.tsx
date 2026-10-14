// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company
import { useEffect, useState } from "react";
import { Link, Outlet, useLocation } from "react-router-dom";
import { api } from "../api/client";
import { useConsole } from "../context/ConsoleContext";
import { S1_INVESTIGATION_ID } from "../mocks/demoData";
import { Explain } from "./Explain";
import { GlossaryScope } from "./GlossaryScope";
import { Led } from "./Chip";
import { LifecycleBar } from "./LifecycleBar";
import { ModeBanner } from "./ModeBanner";
import { Term } from "./Term";

const NAV = [
  { to: "/", label: "Fleet", match: (path: string) => path === "/" },
  { to: "/tools/E-02", label: "Tool", match: (path: string) => path.startsWith("/tools") },
  { to: "/alerts", label: "Alerts", match: (path: string) => path.startsWith("/alerts") },
  { to: "/tickets", label: "Tickets", match: (path: string) => path.startsWith("/tickets") },
  { to: `/investigations/${S1_INVESTIGATION_ID}`, label: "Investigation", match: (path: string) => path.startsWith("/investigations") },
  { to: `/approvals/${S1_INVESTIGATION_ID}`, label: "Approval", match: (path: string) => path.startsWith("/approvals") },
  { to: "/capas", label: "CAPA", match: (path: string) => path.startsWith("/capas") },
  { to: "/injector", label: "Injector", match: (path: string) => path.startsWith("/injector") },
  { to: "/roi", label: "ROI", match: (path: string) => path.startsWith("/roi") },
  { to: "/metrics", label: "Metrics", match: (path: string) => path.startsWith("/metrics") },
  { to: "/glossary", label: "Glossary", match: (path: string) => path.startsWith("/glossary") },
];

function NavLabel({ label }: { label: string }) {
  if (label === "CAPA" || label === "ROI") return <Term id={label} spellOut={false} />;
  return label;
}

export function AppShell() {
  const location = useLocation();
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
    <GlossaryScope key={location.pathname}>
    <div className="flex min-h-screen flex-col bg-mps-canvas text-mps-text">
      <a className="sr-only focus:not-sr-only focus:absolute focus:left-2 focus:top-2 focus:z-toast focus:bg-mps-panel focus:p-2" href="#main">
        Skip to content
      </a>
      <ModeBanner />
      <header className="flex flex-wrap items-center gap-4 border-b border-mps-line bg-mps-panel px-4 py-2">
        <Link to="/" className="flex items-center gap-2.5 text-mps-text no-underline hover:no-underline">
          <img src="/logo-concept-1.svg" width={32} height={32} alt="Meridian Precision Systems mark" />
          <span>
            <strong className="block text-mps-body">Meridian Precision Systems</strong>
            <span className="block font-mono text-mps-micro uppercase tracking-wider text-mps-muted">Fab Ops Console · Austin Demo Fab</span>
          </span>
        </Link>
        <div className="ml-auto flex flex-wrap gap-4 font-mono text-mps-micro text-mps-muted">
          <span>
            <Led tone={healthOn ? "nominal" : "off"} /> Sim clock · 1 s = 1 min
          </span>
          <span>
            Plant <b className="font-medium text-mps-secondary">AUS-1</b>
          </span>
          <span>
            Engineer <b className="font-medium text-mps-secondary">dev-user</b>
          </span>
        </div>
      </header>
      <nav aria-label="Console" className="sticky top-9 z-nav flex gap-1 overflow-x-auto border-b border-mps-line bg-mps-panel px-3">
        {NAV.map((item) => {
          const active = item.match(location.pathname);
          return (
            <Link
              key={item.label}
              to={item.to}
              aria-current={active ? "page" : undefined}
              className={`whitespace-nowrap border-b-2 px-3 py-2 text-mps-body no-underline ${active ? "border-mps-nominal text-mps-nominal" : "border-transparent text-mps-muted hover:text-mps-text"}`}
            >
              <NavLabel label={item.label} />
            </Link>
          );
        })}
      </nav>
      <LifecycleBar current={lifecycle.current} escalated={lifecycle.escalated} />
      <main id="main" className="mx-auto w-full max-w-[1200px] flex-1 px-4 py-4">
        <Outlet />
      </main>
      <footer className="flex flex-wrap items-center justify-between gap-3 border-t border-mps-line px-4 py-3 text-mps-micro text-mps-muted">
        <span className="font-pitch text-mps-body font-bold tracking-wide text-sn-gold" title="Placeholder. Official StatusNeo artwork is not in this repo.">
          Powered by StatusNeo
        </span>
        <span>
          Copyright (c) 2026 Bryan C. Weis and Common Consulting Company · <Explain text="MIT" /> · Fictional demonstration. All companies and data are synthetic.
        </span>
        <a className="text-mps-nominal" href="https://github.com/ccc-bcweis/mps-quality-agents">
          How we built it
        </a>
      </footer>
    </div>
    </GlossaryScope>
  );
}
