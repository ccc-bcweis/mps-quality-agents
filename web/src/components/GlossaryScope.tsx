// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company
import { createContext, useContext, useId, useRef, type ReactNode } from "react";

const FirstUseContext = createContext<Map<string, string> | null>(null);

export function GlossaryScope({ children }: { children: ReactNode }) {
  const seen = useRef(new Map<string, string>());
  return <FirstUseContext.Provider value={seen.current}>{children}</FirstUseContext.Provider>;
}

/** The first enabled term in tree order spells out. Later uses on the same screen stay short. */
export function useFirstUse(term: string, enabled: boolean): boolean {
  const seen = useContext(FirstUseContext);
  const id = useId();
  if (!enabled || !seen) return false;
  const current = seen.get(term);
  if (current === undefined) {
    seen.set(term, id);
    return true;
  }
  return current === id;
}
