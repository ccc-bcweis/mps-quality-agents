// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company
import { formatStamp } from "../api/labels";
import { UserMenu } from "./UserMenu";
import { btnSm } from "./ui";
import { useEffect, useState } from "react";
import { Link, Outlet, useLocation, useNavigate, useOutletContext } from "react-router-dom";
import { ApiProblem, api } from "../api/client";
import { useSimClock } from "../live/useLive";
import { entryRedirect } from "../auth/signOut";
import type { AdminSession } from "../api/types";
import { useConsole } from "../context/ConsoleContext";
import { SiteProvider, useSite } from "../context/SiteContext";
import { CONSOLE_TABS } from "../shell/navigation";
import { plantOptions } from "../sites/selection";
import { countWord, loadCatalog, plants, useCatalog } from "../labels";
import { Explain } from "./Explain";
import { GlossaryScope } from "./GlossaryScope";
import { HarnessBar } from "./HarnessBar";
import { Led } from "./Chip";
import { Term } from "./Term";

export type ShellOutletContext = {
  session: AdminSession | null;
};

/** Plant simulation clock: read once, then the hub's clock message keeps the header current. */
function useSimNow(): string | null {
  return useSimClock();
}

function useAdminSession(): AdminSession | null {
  const [session, setSession] = useState<AdminSession | null>(null);
  useEffect(() => {
    let cancelled = false;
    const here = () => `${window.location.pathname}${window.location.search}`;
    api
      .getAdminSession()
      .then((next) => {
        if (cancelled) return;
        const target = entryRedirect({ session: next }, here());
        if (target) window.location.replace(target);
        else setSession(next);
      })
      .catch((error: unknown) => {
        if (cancelled) return;
        const target = error instanceof ApiProblem ? entryRedirect({ status: error.status }, here()) : null;
        if (target) window.location.replace(target);
        else setSession(null);
      });
    return () => {
      cancelled = true;
    };
  }, []);
  return session;
}

function mainWidth(path: string): string {
  if (path.startsWith("/sites") || path.startsWith("/rules")) return "max-w-[1920px]";
  if (path.startsWith("/tools/")) return "max-w-[1680px]";
  return "max-w-[1200px]";
}

function NavLabel({ label }: { label: string }) {
  if (label === "CAPA") return <Term id="CAPA" spellOut={false} />;
  return label;
}

export function ConsoleFrame() {
  const simNow = useSimNow();
  const location = useLocation();
  const navigate = useNavigate();
  const { session } = useOutletContext<ShellOutletContext>();
  const { dataMode, dataScenarioId } = useConsole();
  const { siteId, setSiteId } = useSite();
  const [healthOn, setHealthOn] = useState(false);
  useCatalog();

  useEffect(() => {
    // Usually loaded before the first render (main.tsx). A signed-out first load retries once the session is known.
    void loadCatalog(api.consoleCatalog).catch(() => undefined);
  }, [session]);

  function chooseSite(next: string) {
    setSiteId(next);
    if (!location.pathname.startsWith("/sites") || next === "all") return;
    const params = new URLSearchParams(location.search);
    if (params.get("site") === next) return;
    params.set("site", next);
    navigate({ pathname: "/sites", search: params.toString() }, { replace: true });
  }

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
      className="flex min-h-0 flex-1 flex-col overflow-hidden bg-mps-canvas font-sans text-mps-text"
    >
      <header className="flex shrink-0 flex-wrap items-center gap-4 border-b border-mps-line bg-mps-panel px-4 py-2">
        <Link to="/" className="flex items-center gap-2.5 text-mps-text no-underline hover:no-underline">
          <img src="/logo-concept-1.svg" width={32} height={32} alt="Meridian Precision Systems mark" />
          <span>
            <strong className="block text-mps-body">Meridian Precision Systems</strong>
            <span className="block font-mono text-mps-micro uppercase tracking-wider text-mps-muted">Fab ops console · {countWord(plants().length)} plants</span>
          </span>
        </Link>
        <div className="ml-auto flex flex-wrap items-center gap-4 font-mono text-mps-micro text-mps-muted">
          <label className="flex shrink-0 items-center gap-2 whitespace-nowrap font-sans text-mps-body text-mps-secondary">
            Plant
            <select
              aria-label="Plant"
              className="shrink-0 whitespace-nowrap rounded border border-mps-line bg-mps-canvas px-2 py-1 font-sans text-mps-body text-mps-text"
              value={siteId}
              onChange={(event) => chooseSite(event.target.value)}
            >
              {plantOptions().map((plant) => (
                <option key={plant.id} value={plant.id}>
                  {plant.name}
                </option>
              ))}
            </select>
          </label>
          <span className="whitespace-nowrap">
            <Led tone={healthOn ? "nominal" : "off"} /> Plant time <b className="font-medium text-mps-secondary" title="Readings are stamped as they arrive, in your local time.">{simNow ? formatStamp(simNow) : "…"}</b>
          </span>
          <span className="whitespace-nowrap">
            Plants <b className="font-medium text-mps-secondary">{plants().length || "…"}</b>
          </span>
          <UserMenu session={session} />
          <Link
            to="/glossary"
            aria-current={location.pathname.startsWith("/glossary") ? "page" : undefined}
            className={btnSm}
          >
            Glossary
          </Link>
        </div>
      </header>
      <nav
        aria-label="Console"
        data-sites-slot="before-fleet"
        className="flex shrink-0 flex-wrap gap-1 border-b border-mps-line bg-mps-panel px-3"
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
      {dataMode === "replay" ? (
        <p
          role="status"
          aria-label="Replay banner"
          data-region="replay-banner"
          className="shrink-0 border-b border-mps-nominal bg-mps-panel px-4 py-2 text-mps-body text-mps-text"
        >
          Replay. The console is showing the recorded run
          {dataScenarioId ? ` for ${dataScenarioId}` : ""}. Switch to Live in the harness to see the running plant.
        </p>
      ) : null}
      <main
        id="main"
        data-scroll="content"
        className={`mx-auto min-h-0 w-full flex-1 overflow-y-auto px-4 py-4 ${mainWidth(location.pathname)}`}
      >
        <Outlet />
      </main>
      <footer className="flex shrink-0 flex-wrap items-center justify-between gap-3 border-t border-mps-line px-4 py-3 text-mps-micro text-mps-muted">
        <span>
          Copyright (c) 2026 Bryan C. Weis and Common Consulting Company · <Explain text="MIT" />
        </span>
        <a className={btnSm} href="https://github.com/ccc-bcweis/mps-quality-agents">
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
      className="flex min-h-0 flex-1 flex-col overflow-hidden rounded border-2 border-dashed border-sn-gold bg-[#07111f] font-sans text-mps-text"
    >
      <p className="shrink-0 border-b border-sn-gold/40 px-4 py-2 font-pitch text-mps-body text-sn-gold">
        Presenter tool. This is not a Meridian Precision Systems console screen.{" "}
        <Link className="whitespace-nowrap rounded border border-sn-gold px-2 py-0.5 text-sn-gold no-underline hover:bg-sn-navy-soft" to="/">
          Open the console
        </Link>
      </p>
      <main id="main" data-scroll="content" className="mx-auto min-h-0 w-full max-w-[1200px] flex-1 overflow-y-auto px-4 py-4">
        <Outlet />
      </main>
      <footer className="shrink-0 px-4 py-3 font-sans text-mps-micro text-sn-gold-wash">
        Copyright (c) 2026 Bryan C. Weis and Common Consulting Company · <Explain text="MIT" />
      </footer>
    </section>
  );
}

export function AppShell() {
  const location = useLocation();
  const session = useAdminSession();
  const { revision } = useConsole();
  const outlet: ShellOutletContext = { session };
  return (
    <SiteProvider>
      <GlossaryScope key={location.pathname}>
      <div className="flex h-full min-h-0 flex-col overflow-hidden bg-sn-navy" data-region="app">
        <a className="sr-only focus:not-sr-only focus:absolute focus:left-2 focus:top-2 focus:z-toast focus:bg-mps-panel focus:p-2" href="#main">
          Skip to content
        </a>
        <HarnessBar session={session} />
        <div className="flex min-h-0 flex-1 flex-col overflow-hidden px-2 pb-2 pt-2">
          <Outlet context={outlet} key={revision} />
        </div>
      </div>
      </GlossaryScope>
    </SiteProvider>
  );
}
