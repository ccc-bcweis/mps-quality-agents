// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company
import { useEffect, useRef, useState } from "react";

/** Wall-clock poll. The API tick defaults to 2 seconds, so the screen checks between ticks. */
export const LIVE_POLL_MS = 2500;

export function usePolled<T>(load: () => Promise<T>, key: string, ms = LIVE_POLL_MS): T | null {
  const [value, setValue] = useState<T | null>(null);
  const loadRef = useRef(load);
  loadRef.current = load;

  useEffect(() => {
    setValue(null);
    let cancelled = false;
    let timer = 0;
    const tick = () => {
      void loadRef
        .current()
        .then((next) => {
          if (!cancelled) setValue(next);
        })
        .catch(() => {
          // Keep the last good snapshot. An empty live list is a no-op for the tiles.
        })
        .finally(() => {
          if (!cancelled) timer = window.setTimeout(tick, ms);
        });
    };
    tick();
    return () => {
      cancelled = true;
      window.clearTimeout(timer);
    };
  }, [key, ms]);

  return value;
}
