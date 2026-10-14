// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

export async function pollUntil<T>(
  load: () => Promise<T>,
  ready: (value: T) => boolean,
  options: {
    timeoutMs: number;
    intervalMs: number;
    sleep?: (ms: number) => Promise<void>;
    now?: () => number;
  },
): Promise<T | null> {
  const sleep = options.sleep ?? ((ms: number) => new Promise((resolve) => setTimeout(resolve, ms)));
  const now = options.now ?? Date.now;
  const start = now();
  let value = await load();
  while (!ready(value)) {
    if (now() - start >= options.timeoutMs) return null;
    await sleep(options.intervalMs);
    value = await load();
  }
  return value;
}
