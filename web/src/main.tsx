// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company
import { StrictMode } from "react";
import { createRoot } from "react-dom/client";
import { App } from "./App";
import { api } from "./api/client";
import { loadCatalog } from "./labels";
import "./index.css";

async function enableMocks(): Promise<void> {
  if (!import.meta.env.DEV) return;
  if (import.meta.env.VITE_USE_MSW === "false") return;
  const { worker } = await import("./mocks/browser");
  await worker.start({ onUnhandledRequest: "bypass" });
}

const root = document.getElementById("root");
if (!root) throw new Error("Root element is missing.");

// Labels, limits and rule sentences come from the server. A failed load (signed out) is retried by the console shell.
void enableMocks()
  .then(() => loadCatalog(api.consoleCatalog).catch(() => undefined))
  .then(() => {
    createRoot(root).render(
      <StrictMode>
        <App />
      </StrictMode>,
    );
  });
