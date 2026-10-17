// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company
import { HubConnectionBuilder, HubConnectionState, LogLevel, type HubConnection } from "@microsoft/signalr";
import { useEffect, useRef } from "react";

/** Record events the server pushes on /hubs/fleet (see FleetHub). */
export const RECORD_EVENTS = ["alertRaised", "ticketOpened", "incidentChanged", "investigationChanged", "capaChanged"] as const;

export type RecordEvent = (typeof RECORD_EVENTS)[number];

type Listener = (event: RecordEvent, payload: unknown) => void;

let connection: HubConnection | null = null;
const listeners = new Set<Listener>();

/** Tests run without a server; the hub stays off there and pages fall back to their first load. */
function hubEnabled(): boolean {
  return import.meta.env.MODE !== "test";
}

function ensureConnection(): void {
  if (connection || !hubEnabled()) return;
  const hub = new HubConnectionBuilder().withUrl("/hubs/fleet").withAutomaticReconnect().configureLogging(LogLevel.Warning).build();
  for (const name of RECORD_EVENTS) {
    hub.on(name, (payload: unknown) => {
      for (const listener of [...listeners]) listener(name, payload);
    });
  }
  // A reconnect may have missed events: tell every page to reload once.
  hub.onreconnected(() => {
    for (const listener of [...listeners]) listener("incidentChanged", null);
  });
  connection = hub;
  void hub.start().catch(() => {
    // The page keeps its last load; automatic reconnect only covers drops after a start, so retry the start later.
    connection = null;
    window.setTimeout(() => {
      if (listeners.size > 0) ensureConnection();
    }, 5000);
  });
}

export function subscribeRecordEvents(listener: Listener): () => void {
  listeners.add(listener);
  ensureConnection();
  return () => {
    listeners.delete(listener);
    if (listeners.size === 0 && connection && connection.state !== HubConnectionState.Disconnected) {
      void connection.stop();
      connection = null;
    }
  };
}

/** Calls <paramref name="onChange"/> whenever an alert, ticket, incident, investigation or CAPA changes. */
export function useRecordEvents(onChange: (event: RecordEvent, payload: unknown) => void): void {
  const ref = useRef(onChange);
  ref.current = onChange;
  useEffect(() => subscribeRecordEvents((event, payload) => ref.current(event, payload)), []);
}
