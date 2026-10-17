// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company
import { HubConnectionBuilder, HubConnectionState, LogLevel, type HubConnection } from "@microsoft/signalr";
import { useEffect, useRef, useState } from "react";

/**
 * Record events the server pushes on /hubs/fleet (see FleetHub). Each carries the changed record, so a page patches
 * its list instead of downloading it again. alertCleared carries { alertId, toolId, clearedAt }.
 */
export const RECORD_EVENTS = [
  "alertRaised",
  "alertCleared",
  "ticketOpened",
  "ticketChanged",
  "incidentChanged",
  "investigationChanged",
  "capaChanged",
  "capaAdvanced",
] as const;

export type RecordEvent = (typeof RECORD_EVENTS)[number];

/** Live messages: toolLive and fleetLive go only to pages that watch a tool or plant; the rest go to every page. */
export const LIVE_EVENTS = ["toolLive", "fleetLive", "clock", "resetProgress", "resync"] as const;

export type HubEvent = RecordEvent | (typeof LIVE_EVENTS)[number];

/** Slow reload while the stream is down; a page never polls while it is connected. */
export const FALLBACK_POLL_MS = 15000;

type Handler = (payload: unknown) => void;
type StateListener = (connected: boolean, reconnected: boolean) => void;
type Watch = { method: "SubscribeTool" | "SubscribeSite"; arg: string; count: number };

let connection: HubConnection | null = null;
let connected = false;
const handlers = new Map<string, Set<Handler>>();
const stateListeners = new Set<StateListener>();
const watches = new Map<string, Watch>();

/** Tests run without a server; the hub stays off there and tests drive it with emitHubEvent / setHubConnected. */
function hubEnabled(): boolean {
  return import.meta.env.MODE !== "test";
}

function users(): number {
  let count = stateListeners.size;
  for (const set of handlers.values()) count += set.size;
  return count;
}

function dispatch(name: string, payload: unknown): void {
  for (const handler of [...(handlers.get(name) ?? [])]) handler(payload);
}

function setConnected(next: boolean, reconnected: boolean): void {
  connected = next;
  for (const listener of [...stateListeners]) listener(next, reconnected);
}

function invokeWatch(watch: Watch, subscribe: boolean): void {
  if (!connection || connection.state !== HubConnectionState.Connected) return;
  const method = subscribe ? watch.method : (watch.method.replace("Subscribe", "Unsubscribe") as string);
  void connection.invoke(method, watch.arg).catch(() => {
    // The next reconnect subscribes again.
  });
}

function ensureConnection(): void {
  if (connection || !hubEnabled()) return;
  const hub = new HubConnectionBuilder().withUrl("/hubs/fleet").withAutomaticReconnect().configureLogging(LogLevel.Warning).build();
  for (const name of [...RECORD_EVENTS, ...LIVE_EVENTS]) hub.on(name, (payload: unknown) => dispatch(name, payload));
  hub.onreconnecting(() => setConnected(false, false));
  // A reconnect may have missed events: subscribe again and tell every page to reload once.
  hub.onreconnected(() => {
    for (const watch of watches.values()) invokeWatch(watch, true);
    setConnected(true, true);
  });
  hub.onclose(() => {
    setConnected(false, false);
    if (connection === hub) connection = null;
    retryLater();
  });
  connection = hub;
  void hub
    .start()
    .then(() => {
      for (const watch of watches.values()) invokeWatch(watch, true);
      setConnected(true, false);
    })
    .catch(() => {
      // The page keeps its last load; automatic reconnect only covers drops after a start, so retry the start later.
      if (connection === hub) connection = null;
      retryLater();
    });
}

function retryLater(): void {
  window.setTimeout(() => {
    if (users() > 0) ensureConnection();
  }, 5000);
}

function release(): void {
  if (users() === 0 && watches.size === 0 && connection && connection.state !== HubConnectionState.Disconnected) {
    const closing = connection;
    connection = null;
    connected = false;
    void closing.stop();
  }
}

/** Listens to one hub message. Returns the unsubscribe. */
export function onHub(name: HubEvent, handler: Handler): () => void {
  let set = handlers.get(name);
  if (!set) {
    set = new Set();
    handlers.set(name, set);
  }
  set.add(handler);
  ensureConnection();
  return () => {
    handlers.get(name)?.delete(handler);
    release();
  };
}

/** Connected / disconnected changes. reconnected is true when the stream came back after a drop. */
export function onHubState(listener: StateListener): () => void {
  stateListeners.add(listener);
  ensureConnection();
  return () => {
    stateListeners.delete(listener);
    release();
  };
}

export function hubConnected(): boolean {
  return connected;
}

/** Joins the server group for one tool or plant so its toolLive / fleetLive messages reach this page. */
export function watchHub(method: Watch["method"], arg: string): () => void {
  const key = `${method}|${arg}`;
  const existing = watches.get(key);
  if (existing) {
    existing.count += 1;
  } else {
    const watch: Watch = { method, arg, count: 1 };
    watches.set(key, watch);
    ensureConnection();
    invokeWatch(watch, true);
  }
  return () => {
    const watch = watches.get(key);
    if (!watch) return;
    watch.count -= 1;
    if (watch.count > 0) return;
    watches.delete(key);
    invokeWatch(watch, false);
    release();
  };
}

/** Test seam: deliver a message as if the server had sent it. */
export function emitHubEvent(name: HubEvent, payload: unknown): void {
  dispatch(name, payload);
}

/** Test seam: flip the connection state. */
export function setHubConnected(next: boolean, reconnected = false): void {
  setConnected(next, reconnected);
}

/** Test seam: the groups this page has joined. */
export function watchedGroups(): string[] {
  return [...watches.keys()];
}

export function subscribeRecordEvents(listener: (event: RecordEvent, payload: unknown) => void): () => void {
  const offs = RECORD_EVENTS.map((name) => onHub(name, (payload) => listener(name, payload)));
  return () => {
    for (const off of offs) off();
  };
}

/** Calls onChange whenever an alert, ticket, incident, investigation or CAPA changes. */
export function useRecordEvents(onChange: (event: RecordEvent, payload: unknown) => void): void {
  const ref = useRef(onChange);
  ref.current = onChange;
  useEffect(() => subscribeRecordEvents((event, payload) => ref.current(event, payload)), []);
}

/** Calls handler for one hub message while the component is mounted. */
export function useHubEvent(name: HubEvent, handler: Handler): void {
  const ref = useRef(handler);
  ref.current = handler;
  useEffect(() => onHub(name, (payload) => ref.current(payload)), [name]);
}

/** True while the stream is connected. */
export function useHubConnected(): boolean {
  const [state, setState] = useState(connected);
  useEffect(() => onHubState((next) => setState(next)), []);
  return state;
}
