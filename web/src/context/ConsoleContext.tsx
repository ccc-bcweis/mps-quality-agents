// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company
import { createContext, useCallback, useContext, useEffect, useLayoutEffect, useMemo, useState, type ReactNode } from "react";
import { api } from "../api/client";
import type { Alert, Capa, DataMode, MetricsSummary, ModelStatus, RunMode } from "../api/types";
import type { LifecycleView } from "../lifecycle";
import { onHub } from "../live/fleetHub";

/** Live when a real model (OpenAI or Azure OpenAI) is answering; replay for offline saved answers or a fallback. */
export type BannerMode = "replay" | "live";

const CAPA_KEY = "mps.capas";

type ConsoleValue = {
  metrics: MetricsSummary | null;
  metricsError: string | null;
  bannerMode: BannerMode;
  serverMode: RunMode | null;
  capReached: boolean;
  /** The model the server is actually using, from GET /api/model/status. Null until it loads. */
  modelStatus: ModelStatus | null;
  lifecycle: LifecycleView;
  setLifecycle: (view: LifecycleView) => void;
  capas: Capa[];
  rememberCapa: (capa: Capa) => void;
  /** Drop remembered CAPAs the server no longer has (another client reset the demo). */
  syncCapas: (serverIds: string[]) => void;
  localAlerts: Alert[];
  rememberLocalAlert: (alert: Alert) => void;
  revision: number;
  clearDemo: () => void;
  dataMode: DataMode["mode"];
  dataScenarioId: string | null;
  setDataMode: (mode: DataMode["mode"], scenarioId?: string) => Promise<DataMode>;
};

const ConsoleContext = createContext<ConsoleValue | null>(null);

function readCapas(): Capa[] {
  try {
    const raw = sessionStorage.getItem(CAPA_KEY);
    if (!raw) return [];
    const parsed = JSON.parse(raw) as Capa[];
    return Array.isArray(parsed) ? parsed : [];
  } catch {
    return [];
  }
}

export function ConsoleProvider({ children }: { children: ReactNode }) {
  const [metrics, setMetrics] = useState<MetricsSummary | null>(null);
  const [metricsError, setMetricsError] = useState<string | null>(null);
  const [modelStatus, setModelStatus] = useState<ModelStatus | null>(null);
  const [lifecycle, setLifecycleState] = useState<LifecycleView>({ current: "alert", escalated: false });
  const [capas, setCapas] = useState<Capa[]>(() => readCapas());
  const [localAlerts, setLocalAlerts] = useState<Alert[]>([]);
  const [revision, setRevision] = useState(0);
  const [plantData, setPlantData] = useState<DataMode>({ mode: "live", scenarioId: null, messageCount: 0 });

  useEffect(() => {
    let cancelled = false;
    api
      .getDataMode()
      .then((next) => {
        if (cancelled) return;
        setPlantData(next);
        if (next.mode === "replay") setRevision((current) => current + 1);
      })
      .catch(() => {
        // Live stays the view until a switch succeeds.
      });
    return () => {
      cancelled = true;
    };
  }, []);

  useEffect(() => {
    let cancelled = false;
    api
      .getMetrics()
      .then((next) => {
        if (!cancelled) setMetrics(next);
      })
      .catch((error: unknown) => {
        if (!cancelled) setMetricsError(error instanceof Error ? error.message : "Metrics are unavailable.");
      });
    return () => {
      cancelled = true;
    };
  }, []);

  // The model status changes only when a live call fails or recovers, which happens inside an investigation: reload it
  // when the server pushes an investigation change instead of polling.
  const liveModel = modelStatus?.live === true;
  useEffect(() => {
    let cancelled = false;
    const load = () => {
      api
        .getModelStatus()
        .then((next) => {
          if (!cancelled) setModelStatus(next);
        })
        .catch(() => {
          // The banner shows the metrics mode until the status loads.
        });
    };
    load();
    const off = liveModel ? onHub("investigationChanged", load) : () => undefined;
    return () => {
      cancelled = true;
      off();
    };
  }, [liveModel]);

  const capReached = metrics !== null && metrics.spendCapUsd > 0 && metrics.spendTodayUsd >= metrics.spendCapUsd;
  const serverMode = metrics?.mode ?? null;
  const bannerMode: BannerMode =
    capReached || modelStatus?.fallingBack ? "replay" : modelStatus ? (modelStatus.live ? "live" : "replay") : (serverMode ?? "replay");

  const setLifecycle = useCallback((view: LifecycleView) => {
    setLifecycleState(view);
  }, []);

  const rememberCapa = useCallback((capa: Capa) => {
    setCapas((current) => {
      const next = [capa, ...current.filter((item) => item.id !== capa.id)];
      sessionStorage.setItem(CAPA_KEY, JSON.stringify(next));
      return next;
    });
  }, []);

  const syncCapas = useCallback((serverIds: string[]) => {
    setCapas((current) => {
      const keep = current.filter((item) => serverIds.includes(item.id));
      if (keep.length === current.length) return current;
      if (keep.length === 0) sessionStorage.removeItem(CAPA_KEY);
      else sessionStorage.setItem(CAPA_KEY, JSON.stringify(keep));
      return keep;
    });
  }, []);

  const rememberLocalAlert = useCallback((alert: Alert) => {
    setLocalAlerts((current) => [alert, ...current.filter((item) => item.correlationId !== alert.correlationId)]);
  }, []);

  const clearDemo = useCallback(() => {
    sessionStorage.removeItem(CAPA_KEY);
    setCapas([]);
    setLocalAlerts([]);
    setRevision((current) => current + 1);
  }, []);

  const setDataMode = useCallback(async (mode: DataMode["mode"], scenarioId?: string) => {
    const next = await api.setDataMode(mode, scenarioId);
    setPlantData(next);
    setRevision((current) => current + 1);
    return next;
  }, []);

  const value = useMemo<ConsoleValue>(
    () => ({
      metrics,
      metricsError,
      bannerMode,
      serverMode,
      capReached,
      modelStatus,
      lifecycle,
      setLifecycle,
      capas,
      rememberCapa,
      syncCapas,
      localAlerts,
      rememberLocalAlert,
      revision,
      clearDemo,
      dataMode: plantData.mode,
      dataScenarioId: plantData.scenarioId ?? null,
      setDataMode,
    }),
    [
      metrics,
      metricsError,
      bannerMode,
      serverMode,
      capReached,
      modelStatus,
      lifecycle,
      setLifecycle,
      capas,
      rememberCapa,
      syncCapas,
      localAlerts,
      rememberLocalAlert,
      revision,
      clearDemo,
      plantData,
      setDataMode,
    ],
  );

  return <ConsoleContext.Provider value={value}>{children}</ConsoleContext.Provider>;
}

export function useConsole(): ConsoleValue {
  const value = useContext(ConsoleContext);
  if (!value) throw new Error("useConsole must be used inside ConsoleProvider.");
  return value;
}

export function useSetLifecycle(current: LifecycleView["current"], escalated = false): void {
  const { setLifecycle } = useConsole();
  useLayoutEffect(() => {
    setLifecycle({ current, escalated });
  }, [current, escalated, setLifecycle]);
}
