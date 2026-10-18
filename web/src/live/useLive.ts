// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company
import { useEffect, useRef, useState, type Dispatch, type SetStateAction } from "react";
import { api } from "../api/client";
import type { Alert, Capa, FleetLive, Incident, Investigation, Ticket, ToolLive } from "../api/types";
import { toolInPlant } from "../sites/selection";
import { FALLBACK_POLL_MS, hubConnected, onHub, onHubState, watchHub, type HubEvent } from "./fleetHub";
import { applyFleetDelta, applyToolDelta, clearAlert, isRecord, upsertRecord, type AlertCleared, type FleetLiveDelta, type ToolLiveDelta } from "./stream";

/** What a message does to the loaded value: a new value, the same value, or "reload" to load it again. */
type Apply<T> = (previous: T | null, payload: unknown) => T | null | "reload";

type LiveOptions<T> = {
  watch?: { method: "SubscribeTool" | "SubscribeSite"; arg: string };
  events?: { name: HubEvent; apply: Apply<T> }[];
};

/**
 * Loads once, then follows the fleet hub: each message patches the value. A resync or a reconnect loads it again.
 * While the stream is down the value reloads every FALLBACK_POLL_MS; while it is up nothing polls.
 */
export function useLiveResource<T>(key: string, load: () => Promise<T>, options: LiveOptions<T> = {}): T | null {
  const [value, setValue] = useState<T | null>(null);
  const loadRef = useRef(load);
  loadRef.current = load;
  const optionsRef = useRef(options);
  optionsRef.current = options;
  const watchMethod = options.watch?.method;
  const watchArg = options.watch?.arg;

  useEffect(() => {
    setValue(null);
    let cancelled = false;
    let timer = 0;
    let latest: T | null = null;
    // Only the newest load applies, and messages that arrive while it runs are replayed on top of it. Before, every
    // message that found no loaded value started another load; the loads answered out of order, so the chart swapped
    // between older and newer windows and back.
    let requested = 0;
    let loading = false;
    let reloadAgain = false;
    let queued: { apply: Apply<T>; payload: unknown }[] = [];
    const reload = () => {
      if (loading) {
        reloadAgain = true;
        return;
      }
      loading = true;
      const ticket = ++requested;
      void loadRef
        .current()
        .then((next) => {
          if (cancelled || ticket !== requested) return;
          let value: T | null = next;
          const pending = queued;
          queued = [];
          for (const { apply, payload } of pending) {
            const patched = apply(value, payload);
            if (patched === "reload") {
              reloadAgain = true;
              break;
            }
            value = patched;
          }
          latest = value;
          setValue(value);
        })
        .catch(() => {
          // Keep the last good value; the stream or the fallback tries again.
        })
        .finally(() => {
          if (ticket !== requested) return;
          loading = false;
          if (reloadAgain && !cancelled) {
            reloadAgain = false;
            queued = [];
            reload();
          }
        });
    };
    const schedule = () => {
      window.clearInterval(timer);
      timer = hubConnected() ? 0 : window.setInterval(reload, FALLBACK_POLL_MS);
    };
    reload();
    schedule();
    const offs: (() => void)[] = [];
    if (watchMethod && watchArg) offs.push(watchHub(watchMethod, watchArg));
    offs.push(
      onHubState((_connected, reconnected) => {
        schedule();
        if (reconnected) reload();
      }),
    );
    offs.push(onHub("resync", reload));
    for (const { name } of optionsRef.current.events ?? []) {
      offs.push(
        onHub(name, (payload) => {
          const handler = optionsRef.current.events?.find((event) => event.name === name);
          if (!handler || cancelled) return;
          if (loading) {
            queued.push({ apply: handler.apply, payload });
            return;
          }
          const next = handler.apply(latest, payload);
          if (next === "reload") {
            reload();
            return;
          }
          if (next !== latest) {
            latest = next;
            setValue(next);
          }
        }),
      );
    }
    return () => {
      cancelled = true;
      window.clearInterval(timer);
      for (const off of offs) off();
    };
    // The event list is read through a ref; the key names the resource.
  }, [key, watchMethod, watchArg]);

  return value;
}

/** One tool's live readings and chart series from the toolLive stream. */
export function useToolLive(toolId: string): ToolLive | null {
  return useLiveResource(`tool-live:${toolId}`, () => api.getToolLive(toolId), {
    watch: { method: "SubscribeTool", arg: toolId },
    events: [
      {
        name: "toolLive",
        apply: (previous, payload) => {
          const delta = payload as ToolLiveDelta;
          if (!delta || delta.toolId !== toolId) return previous;
          return applyToolDelta(previous, delta) ?? "reload";
        },
      },
    ],
  });
}

/** Live tiles for one plant (or "all") from the fleetLive stream. */
export function useFleetLive(siteId: string): FleetLive | null {
  return useLiveResource(`fleet-live:${siteId}`, () => api.listFleetLive(), {
    watch: { method: "SubscribeSite", arg: siteId || "all" },
    events: [{ name: "fleetLive", apply: (previous, payload) => applyFleetDelta(previous, payload as FleetLiveDelta) ?? "reload" }],
  });
}

/** The plant clock in the header: one read, then the clock message. */
export function useSimClock(): string | null {
  const live = useLiveResource<{ simulatedNow: string } | null>("clock", () => api.listFleetLive(), {
    events: [
      {
        name: "clock",
        apply: (previous, payload) => {
          const next = (payload as { simulatedNow?: string } | null)?.simulatedNow;
          return next ? { simulatedNow: next } : previous;
        },
      },
    ],
  });
  return live?.simulatedNow ?? null;
}

type Setter<T> = Dispatch<SetStateAction<T[]>>;

/** The list setters a page owns; each one it passes is patched from the record events. */
export type RecordSetters = {
  alerts?: Setter<Alert>;
  tickets?: Setter<Ticket>;
  incidents?: Setter<Incident>;
  investigations?: Setter<Investigation>;
  capas?: Setter<Capa>;
};

/**
 * Patches a page's record lists from the fleet hub: a new or changed record is put in place, a cleared alert is
 * marked cleared. Records at another plant, or another tool when toolId is set, are skipped. A resync or reconnect
 * calls onReload (a reset wiped the records, or messages may have been missed).
 */
export function useRecordPatches(setters: RecordSetters, scope: { siteId?: string; toolId?: string; onReload?: () => void } = {}): void {
  const settersRef = useRef(setters);
  settersRef.current = setters;
  const scopeRef = useRef(scope);
  scopeRef.current = scope;

  useEffect(() => {
    const inScope = (record: { toolId?: string | null; siteId?: string | null }): boolean => {
      const { siteId, toolId } = scopeRef.current;
      if (toolId && record.toolId && record.toolId !== toolId) return false;
      if (siteId && record.toolId && !toolInPlant(siteId, record.toolId, record.siteId ?? undefined)) return false;
      return true;
    };
    const patch = <T extends { id: string }>(pick: (all: RecordSetters) => Setter<T> | undefined) => (payload: unknown) => {
      const setter = pick(settersRef.current);
      if (!setter || !isRecord(payload) || !inScope(payload)) return;
      setter((list) => upsertRecord(list, payload as unknown as T));
    };
    const reload = () => scopeRef.current.onReload?.();
    const offs = [
      onHub("alertRaised", patch((all) => all.alerts)),
      onHub("alertCleared", (payload) => {
        const cleared = payload as AlertCleared | null;
        if (!cleared?.alertId) return;
        settersRef.current.alerts?.((list) => clearAlert(list, cleared));
      }),
      onHub("ticketOpened", patch((all) => all.tickets)),
      onHub("ticketChanged", patch((all) => all.tickets)),
      onHub("incidentChanged", patch((all) => all.incidents)),
      onHub("investigationChanged", patch((all) => all.investigations)),
      onHub("capaChanged", patch((all) => all.capas)),
      onHub("capaAdvanced", patch((all) => all.capas)),
      onHub("resync", reload),
      onHubState((_connected, reconnected) => {
        if (reconnected) reload();
      }),
    ];
    return () => {
      for (const off of offs) off();
    };
  }, []);
}
