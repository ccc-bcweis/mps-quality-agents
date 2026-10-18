// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company
import { JoinLinksDialog } from "./JoinLinks";
import { useEffect, useState } from "react";
import { SpeedControl } from "./SpeedControl";
import { HARNESS_REFRESH_EVENT, readRunning, readScenario, refreshHarness, RUNNING_EVENT, storeRunning, storeScenario } from "./harnessSession";
import { Link, useLocation, useNavigate } from "react-router-dom";
import { api, ApiProblem } from "../api/client";
import { hubConnected, onHub } from "../live/fleetHub";
import type { AdminSession, ResetKind, ScriptedScenario } from "../api/types";
import { useConsole } from "../context/ConsoleContext";
import { HARNESS_LINKS } from "../shell/navigation";
import { HarnessPrintout } from "./HarnessPrintout";
import { ModeBanner } from "./ModeBanner";
import { ScenarioControl } from "./ScenarioControl";
import { Term } from "./Term";
import { btn } from "./ui";

/** Reset progress read while the hub is down; a reset lasts seconds, so this is faster than the page fallback. */
const RESET_FALLBACK_POLL_MS = 2000;

/** Missing or any other value means collapsed. Each browser remembers its own choice. */
export const HARNESS_STORAGE_KEY = "mps.harness";

const DATA_TIP =
  "Live follows the running plant. Replay shows a recorded scenario. This does not change how the AI answers.";
const REPLAY_TIP =
  "Show the recorded messages for the scenario picked above. The recording stays when you switch back to Live.";
const HARD_TIP =
  "Wipes tickets, alerts, and history, then fills a week of healthy readings. Company rules stay.";
const SOFT_TIP =
  "Closes open tickets and investigations as Closed by demo reset. Deletes nothing. History stays, and every tool returns to nominal.";

const HARD_COPY =
  "Hard reset wipes tickets, alerts, investigations, and history, then fills a week of healthy readings. Company rules stay.";
const SOFT_COPY =
  "Soft reset closes open tickets and investigations as Closed by demo reset. It deletes nothing. History stays, and every tool returns to nominal.";

const resetBtn =
  "inline-flex shrink-0 items-center whitespace-nowrap rounded bg-sn-gold px-3 py-1.5 font-pitch text-mps-body font-semibold text-sn-gold-ink hover:brightness-110 focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-sn-gold disabled:cursor-not-allowed disabled:opacity-40";

const softBtn =
  "inline-flex shrink-0 items-center whitespace-nowrap rounded border border-sn-gold bg-transparent px-3 py-1.5 font-pitch text-mps-body font-semibold text-sn-gold hover:bg-sn-navy-soft focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-sn-gold disabled:cursor-not-allowed disabled:opacity-40";

const modeBtn = (on: boolean) =>
  `shrink-0 whitespace-nowrap rounded border px-2 py-0.5 ${on ? "border-sn-gold bg-sn-navy-soft text-sn-gold" : "border-sn-gold/40 text-sn-gold-wash"} disabled:cursor-not-allowed disabled:opacity-40`;

function readExpanded(): boolean {
  try {
    return localStorage.getItem(HARNESS_STORAGE_KEY) === "expanded";
  } catch {
    return false;
  }
}

export function HarnessBar({ session }: { session: AdminSession | null }) {
  const location = useLocation();
  const navigate = useNavigate();
  const { modelStatus, clearDemo, dataMode, dataScenarioId, setDataMode } =
    useConsole();
  const [expanded, setExpanded] = useState(readExpanded);
  const [scenarios, setScenarios] = useState<ScriptedScenario[]>([]);
  const [scenarioId, setScenarioIdState] = useState(readScenario);
  const [running, setRunning] = useState(readRunning);
  const setScenarioId = (id: string) => {
    storeScenario(id);
    setScenarioIdState(id);
  };
  useEffect(() => {
    const sync = () => setRunning(readRunning());
    window.addEventListener(RUNNING_EVENT, sync);
    return () => window.removeEventListener(RUNNING_EVENT, sync);
  }, []);
  // The running scenario is the server's, so a run, stop or reset from another browser shows here too: read it once,
  // follow the clock message, and read it again after a resync (a reset).
  useEffect(() => {
    let cancelled = false;
    const adopt = (id: string | null | undefined) => {
      const next = id ?? "";
      if (!cancelled && next !== readRunning()) storeRunning(next);
    };
    // Only the newest read applies: a slow answer from before a stop or reset must not bring the old state back.
    let latest = 0;
    const load = () => {
      const ticket = ++latest;
      api
        .getSpeed()
        .then((state) => {
          if (ticket === latest && state && "runningScenarioId" in state) adopt(state.runningScenarioId);
        })
        .catch(() => undefined);
    };
    load();
    const stopClock = onHub("clock", (payload) => {
      const clock = payload as { runningScenarioId?: string | null } | null;
      if (clock && "runningScenarioId" in clock) adopt(clock.runningScenarioId);
    });
    const stopResync = onHub("resync", (payload) => {
      load();
      // After a reset (from any browser) nothing is picked or running.
      if ((payload as { reason?: string } | null)?.reason === "reset" && !cancelled) {
        storeScenario("");
        setScenarioIdState("");
      }
    });
    window.addEventListener(HARNESS_REFRESH_EVENT, load);
    return () => {
      cancelled = true;
      stopClock();
      stopResync();
      window.removeEventListener(HARNESS_REFRESH_EVENT, load);
    };
  }, []);
  const [scenarioError, setScenarioError] = useState<string | null>(null);
  const [resetKind, setResetKind] = useState<ResetKind | null>(null);
  const [resetPending, setResetPending] = useState(false);
  const [resetMessage, setResetMessage] = useState<string | null>(null);
  const [stopPending, setStopPending] = useState(false);

  const stopScenario = async () => {
    setStopPending(true);
    try {
      const result = await api.stopScenario();
      storeRunning("");
      refreshHarness();
      setResetMessage(`Scenario stopped. ${result.removed} fault${result.removed === 1 ? "" : "s"} removed; telemetry returns to nominal.`);
    } catch (reason) {
      setResetMessage(reason instanceof ApiProblem ? reason.title : "The scenario did not stop.");
    } finally {
      setStopPending(false);
    }
  };
  const [dataPending, setDataPending] = useState(false);
  const [dataMessage, setDataMessage] = useState<string | null>(null);

  useEffect(() => {
    let cancelled = false;
    api
      .listScenarios()
      .then((next) => {
        if (cancelled) return;
        setScenarios(next);
        // No default pick: until the presenter chooses, the picker says "Choose a scenario".
        setScenarioIdState((current) => (current && next.some((item) => item.id === current) ? current : ""));
      })
      .catch(() => {
        if (!cancelled) setScenarioError("The scenario list did not load.");
      });
    return () => {
      cancelled = true;
    };
  }, []);

  function toggle() {
    setExpanded((current) => {
      const next = !current;
      try {
        localStorage.setItem(
          HARNESS_STORAGE_KEY,
          next ? "expanded" : "collapsed",
        );
      } catch {
        // A private browser can refuse storage. The row still toggles for this visit.
      }
      return next;
    });
  }

  async function confirmReset() {
    if (!resetKind) return;
    const kind = resetKind;
    setResetKind(null);
    setResetPending(true);
    setResetMessage(null);
    let active = true;
    // Progress streams over the fleet hub (resetProgress). Only when the stream is down does the page read it, slowly.
    const stopProgress = onHub("resetProgress", (payload) => {
      const message = (payload as { message?: string } | null)?.message;
      if (active && message) setResetMessage(message);
    });
    const poll = () => {
      if (hubConnected()) return;
      api
        .getResetProgress()
        .then((progress) => {
          if (active && progress.message) setResetMessage(progress.message);
        })
        .catch(() => {
          // The finished line replaces a missed progress read.
        });
    };
    poll();
    const timer = window.setInterval(poll, RESET_FALLBACK_POLL_MS);
    try {
      const result = await api.resetDemo(kind);
      storeRunning("");
      setScenarioId("");
      refreshHarness();
      active = false;
      clearDemo();
      setResetMessage(
        result.kind === "soft"
          ? `Open work closed. Seed ${result.seed}. History was kept.`
          : `Demo baseline restored. Seed ${result.seed}.`,
      );
      if (result.kind !== "soft") navigate("/");
    } catch {
      active = false;
      setResetMessage("The reset was not accepted.");
    } finally {
      window.clearInterval(timer);
      stopProgress();
      setResetPending(false);
    }
  }

  async function chooseData(mode: "live" | "replay") {
    if (mode === "replay" && !scenarioId) {
      setDataMessage("Pick a scenario before replay.");
      return;
    }
    setDataPending(true);
    setDataMessage(null);
    try {
      await setDataMode(mode, mode === "replay" ? scenarioId : undefined);
    } catch (reason: unknown) {
      setDataMessage(
        reason instanceof ApiProblem
          ? reason.title
          : "The data view was not changed.",
      );
    } finally {
      setDataPending(false);
    }
  }

  // "Running SC-x" only while the server runs one; otherwise just the data mode and the AI engine.
  const activeScenario =
    dataMode === "replay" && dataScenarioId ? dataScenarioId : running ? `Running ${running}` : null;
  const statusLine = [dataMode === "replay" ? "Replay" : "Live", activeScenario, modelStatus?.label ?? "…"]
    .filter(Boolean)
    .join(" · ");
  const admin = session?.isAdmin === true;
  const [joinOpen, setJoinOpen] = useState(false);
  const dialogCopy = resetKind === "soft" ? SOFT_COPY : HARD_COPY;

  return (
    <header
      aria-label="StatusNeo demo harness"
      data-region="harness"
      className="z-banner shrink-0 border-b-4 border-sn-gold bg-sn-navy font-pitch text-sn-gold-wash"
    >
      <div className="flex flex-nowrap items-center gap-x-4 gap-y-2 px-4 py-2">
        <div className="flex shrink-0 flex-col gap-0.5">
          <span
            className="inline-block whitespace-nowrap border border-dashed border-sn-gold px-2 py-0.5 font-pitch text-mps-body font-bold tracking-wide text-sn-gold"
            title="Placeholder. Official StatusNeo artwork is not in this repo."
          >
            Powered by StatusNeo
          </span>
          <span className="whitespace-nowrap font-pitch text-mps-micro uppercase tracking-wider text-sn-gold">
            Demo harness
          </span>
          <span className="whitespace-nowrap font-pitch text-mps-micro text-sn-gold-wash">
            Fictional demonstration. All companies and data are synthetic.
          </span>
        </div>
        <button
          type="button"
          className="inline-flex shrink-0 items-center gap-1 whitespace-nowrap rounded border border-sn-gold px-2 py-1 text-mps-body text-sn-gold hover:bg-sn-navy-soft focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-sn-gold"
          aria-expanded={expanded}
          aria-label={expanded ? "Collapse harness" : "Expand harness"}
          title="Show or hide the presenter controls. The console keeps scrolling on its own."
          onClick={toggle}
        >
          <span aria-hidden="true">{expanded ? "▲" : "▼"}</span>
        </button>
        <p
          className="shrink-0 whitespace-nowrap text-mps-body text-sn-gold-wash"
          role="status"
          aria-label="Harness status"
        >
          {statusLine}
        </p>
      </div>
      {expanded ? (
        <div
          data-region="harness-controls"
          className="grid grid-cols-1 gap-x-4 gap-y-2 border-t border-white/10 px-4 py-2 lg:grid-cols-2"
        >
          <div className="flex min-w-0 flex-wrap content-start items-center gap-x-4 gap-y-2">
            <nav
              aria-label="Demo harness"
              className="flex flex-wrap items-center gap-1"
            >
              {HARNESS_LINKS.map((item) => {
                const active = item.match(location.pathname);
                return (
                  <Link
                    key={item.to}
                    to={item.to}
                    aria-current={active ? "page" : undefined}
                    className={`shrink-0 whitespace-nowrap rounded px-2 py-1 text-mps-body no-underline ${active ? "border border-sn-gold text-sn-gold" : "border border-transparent text-sn-gold-wash hover:text-sn-gold"}`}
                  >
                    {item.label === "Return on investment" ? (
                      <Term id="ROI" spellOut={false}>
                        Return on investment
                      </Term>
                    ) : (
                      item.label
                    )}
                  </Link>
                );
              })}
            </nav>
            <ScenarioControl
              part="controls"
              scenarios={scenarios}
              scenarioId={scenarioId}
              onScenarioId={setScenarioId}
              loadError={scenarioError}
            />
            <HarnessPrintout />
            <button
              type="button"
              className={softBtn}
              title="Remove the running scenario and injected faults. Telemetry returns to nominal. Alerts, tickets and other records stay."
              disabled={stopPending}
              onClick={() => void stopScenario()}
            >
              {stopPending ? "Stopping…" : "Stop scenario"}
            </button>
            {admin ? (
              <div className="flex flex-wrap items-center gap-2">
                <button
                  type="button"
                  className={resetBtn}
                  title={HARD_TIP}
                  disabled={resetPending}
                  onClick={() => setResetKind("hard")}
                >
                  Hard reset
                </button>
                <button
                  type="button"
                  className={softBtn}
                  title={SOFT_TIP}
                  disabled={resetPending}
                  onClick={() => setResetKind("soft")}
                >
                  Soft reset
                </button>
                <button type="button" className={softBtn} title="Invite people with a link that expires and has a use limit." onClick={() => setJoinOpen(true)}>
                  Join links
                </button>
              </div>
            ) : null}
            <div
              role="group"
              aria-label="Plant data"
              title={DATA_TIP}
              className="flex flex-wrap items-center gap-2"
            >
              <span className="shrink-0 whitespace-nowrap text-mps-body">
                Plant data:
              </span>
              <button
                type="button"
                aria-pressed={dataMode === "live"}
                title={DATA_TIP}
                className={modeBtn(dataMode === "live")}
                disabled={!admin || dataPending}
                onClick={() => void chooseData("live")}
              >
                Live
              </button>
              <button
                type="button"
                aria-pressed={dataMode === "replay"}
                title={REPLAY_TIP}
                className={modeBtn(dataMode === "replay")}
                disabled={!admin || dataPending || !scenarioId}
                onClick={() => void chooseData("replay")}
              >
                Replay
              </button>
            </div>
            <SpeedControl admin={admin} />
            <ModeBanner />
            {joinOpen && admin ? <JoinLinksDialog onClose={() => setJoinOpen(false)} /> : null}
            {resetMessage ? (
              <p
                className="basis-full text-mps-body text-sn-gold-wash"
                role="status"
              >
                {resetMessage}
              </p>
            ) : null}
            {dataMessage ? (
              <p
                className="basis-full text-mps-body text-mps-alarm-text"
                role="alert"
              >
                {dataMessage}
              </p>
            ) : null}
          </div>
          <div className="min-w-0">
            <ScenarioControl
              part="card"
              scenarios={scenarios}
              scenarioId={scenarioId}
              onScenarioId={setScenarioId}
              loadError={null}
            />
          </div>
        </div>
      ) : null}
      {resetKind ? (
        <div
          className="fixed inset-0 z-toast flex items-center justify-center bg-black/60 p-4"
          role="presentation"
        >
          <div
            role="dialog"
            aria-modal="true"
            aria-labelledby="reset-title"
            className="w-full max-w-lg rounded border border-sn-gold bg-sn-navy p-4 text-sn-gold-wash shadow-panel"
          >
            <h2
              id="reset-title"
              className="font-pitch text-mps-title font-semibold text-sn-gold"
            >
              {dialogCopy}
            </h2>
            <div className="mt-4 flex flex-wrap gap-2">
              <button
                type="button"
                className={btn}
                disabled={resetPending}
                onClick={() => void confirmReset()}
              >
                {resetPending
                  ? "Resetting…"
                  : resetKind === "soft"
                    ? "Confirm soft reset"
                    : "Confirm hard reset"}
              </button>
              <button
                type="button"
                className={btn}
                disabled={resetPending}
                onClick={() => setResetKind(null)}
              >
                Cancel
              </button>
            </div>
          </div>
        </div>
      ) : null}
    </header>
  );
}
