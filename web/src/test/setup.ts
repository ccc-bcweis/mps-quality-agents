// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company
import { cleanup } from "@testing-library/react";
import "@testing-library/jest-dom/vitest";
import { afterAll, afterEach, beforeAll, beforeEach } from "vitest";
import { setHubConnected } from "../live/fleetHub";
import { setCatalog } from "../labels";
import { resetMockState } from "../mocks/state";
import { storeRunning } from "../components/harnessSession";
import catalogFixture from "./catalog.fixture.json";
import { server } from "./server";

class ResizeObserverStub {
  observe(): void {}
  unobserve(): void {}
  disconnect(): void {}
}

globalThis.ResizeObserver = ResizeObserverStub;

beforeAll(() => server.listen({ onUnhandledRequest: "error" }));
beforeEach(() => {
  // The server catalog (labels, limits, rule sentences), as served by /api/metadata/catalog. ConsoleCatalogTests keeps it equal.
  setCatalog(catalogFixture);
  if (typeof sessionStorage !== "undefined") sessionStorage.clear();
  if (typeof localStorage !== "undefined") {
    localStorage.removeItem("mps.selectedSite");
    localStorage.removeItem("mps.harness");
  }
  resetMockState();
  storeRunning("");
});
afterEach(() => {
  cleanup();
  setHubConnected(false);
  server.resetHandlers();
});
afterAll(() => server.close());
