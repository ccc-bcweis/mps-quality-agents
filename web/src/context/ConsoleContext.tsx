// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company
import { createContext, useCallback, useContext, useEffect, useLayoutEffect, useMemo, useState, type ReactNode } from "react";
import { api } from "../api/client";
import type { Alert, Capa, MetricsSummary, RunMode } from "../api/types";
import type { LifecycleView } from "../lifecycle";

export type BannerMode = "replay" | "live" | "mockReasoner";

const PREVIEW_KEY = "mps.previewMode";
const CAPA_KEY = "mps.capas";

type ConsoleValue = {
  metrics: MetricsSummary | null;
  metricsError: string | null;
  bannerMode: BannerMode;
  serverMode: RunMode | null;
  capReached: boolean;
  localPreview: boolean;
  setPreview: (mode: BannerMode) => void;
  noteUnrecordedInjection: () => void;
  lifecycle: LifecycleView;
  setLifecycle: (view: LifecycleView) => void;
  capas: Capa[];
  rememberCapa: (capa: Capa) => void;
  localAlerts: Alert[];
  rememberLocalAlert: (alert: Alert) => void;
};

const ConsoleContext = createContext<ConsoleValue | null>(null);

function readPreview(): BannerMode | null {
  const stored = sessionStorage.getItem(PREVIEW_KEY);
  if (stored === "replay" || stored === "live" || stored === "mockReasoner") return stored;
  return null;
}

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
  const [preview, setPreviewState] = useState<BannerMode | null>(() => readPreview());
  const [lifecycle, setLifecycleState] = useState<LifecycleView>({ current: "alert", escalated: false });
  const [capas, setCapas] = useState<Capa[]>(() => readCapas());
  const [localAlerts, setLocalAlerts] = useState<Alert[]>([]);

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

  const capReached = metrics !== null && metrics.spendCapUsd > 0 && metrics.spendTodayUsd >= metrics.spendCapUsd;
  const serverMode = metrics?.mode ?? null;
  const bannerMode: BannerMode = capReached ? "replay" : (preview ?? serverMode ?? "replay");
  const localPreview = preview !== null && !capReached && preview !== serverMode && preview !== "mockReasoner";

  const setPreview = useCallback((mode: BannerMode) => {
    sessionStorage.setItem(PREVIEW_KEY, mode);
    setPreviewState(mode);
  }, []);

  const noteUnrecordedInjection = useCallback(() => {
    setPreviewState((current) => {
      const active = current ?? metrics?.mode ?? "replay";
      if (active !== "live") return current;
      sessionStorage.setItem(PREVIEW_KEY, "mockReasoner");
      return "mockReasoner";
    });
  }, [metrics?.mode]);

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

  const rememberLocalAlert = useCallback((alert: Alert) => {
    setLocalAlerts((current) => [alert, ...current.filter((item) => item.correlationId !== alert.correlationId)]);
  }, []);

  const value = useMemo<ConsoleValue>(
    () => ({
      metrics,
      metricsError,
      bannerMode,
      serverMode,
      capReached,
      localPreview: localPreview || (preview === "mockReasoner" && serverMode !== null),
      setPreview,
      noteUnrecordedInjection,
      lifecycle,
      setLifecycle,
      capas,
      rememberCapa,
      localAlerts,
      rememberLocalAlert,
    }),
    [
      metrics,
      metricsError,
      bannerMode,
      serverMode,
      capReached,
      localPreview,
      preview,
      setPreview,
      noteUnrecordedInjection,
      lifecycle,
      setLifecycle,
      capas,
      rememberCapa,
      localAlerts,
      rememberLocalAlert,
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
