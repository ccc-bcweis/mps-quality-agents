// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company
import { cleanup } from "@testing-library/react";
import "@testing-library/jest-dom/vitest";
import { afterAll, afterEach, beforeAll, beforeEach } from "vitest";
import { setHubConnected } from "../live/fleetHub";
import { resetMockState } from "../mocks/state";
import { server } from "./server";

class ResizeObserverStub {
  observe(): void {}
  unobserve(): void {}
  disconnect(): void {}
}

globalThis.ResizeObserver = ResizeObserverStub;

beforeAll(() => server.listen({ onUnhandledRequest: "error" }));
beforeEach(() => {
  if (typeof sessionStorage !== "undefined") sessionStorage.clear();
  if (typeof localStorage !== "undefined") {
    localStorage.removeItem("mps.selectedSite");
    localStorage.removeItem("mps.harness");
  }
  resetMockState();
});
afterEach(() => {
  cleanup();
  setHubConnected(false);
  server.resetHandlers();
});
afterAll(() => server.close());
