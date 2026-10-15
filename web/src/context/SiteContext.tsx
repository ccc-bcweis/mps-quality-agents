// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company
import { createContext, useCallback, useContext, useEffect, useMemo, useState, type ReactNode } from "react";
import { readStoredSite, SITE_STORAGE_KEY, storeSite } from "../sites/selection";

type SiteValue = {
  siteId: string;
  setSiteId: (siteId: string) => void;
};

const SiteContext = createContext<SiteValue | null>(null);

export function SiteProvider({ children }: { children: ReactNode }) {
  const [siteId, setSiteIdState] = useState(readStoredSite);

  const setSiteId = useCallback((next: string) => {
    setSiteIdState(storeSite(next));
  }, []);

  useEffect(() => {
    function onStorage(event: StorageEvent) {
      if (event.key !== SITE_STORAGE_KEY) return;
      setSiteIdState(readStoredSite());
    }
    window.addEventListener("storage", onStorage);
    return () => window.removeEventListener("storage", onStorage);
  }, []);

  const value = useMemo(() => ({ siteId, setSiteId }), [siteId, setSiteId]);
  return <SiteContext.Provider value={value}>{children}</SiteContext.Provider>;
}

export function useSite(): SiteValue {
  const value = useContext(SiteContext);
  if (!value) throw new Error("useSite must be used inside SiteProvider");
  return value;
}
