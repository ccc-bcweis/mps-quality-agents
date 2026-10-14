// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company
import { StrictMode } from "react";
import { createRoot } from "react-dom/client";
import { App } from "./App";
import "./index.css";

async function enableMocks(): Promise<void> {
  if (!import.meta.env.DEV) return;
  if (import.meta.env.VITE_USE_MSW === "false") return;
  const { worker } = await import("./mocks/browser");
  await worker.start({ onUnhandledRequest: "bypass" });
}

const root = document.getElementById("root");
if (!root) throw new Error("Root element is missing.");

void enableMocks().then(() => {
  createRoot(root).render(
    <StrictMode>
      <App />
    </StrictMode>,
  );
});
