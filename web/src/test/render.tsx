// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company
import { render, type RenderResult } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { AppRoutes } from "../App";
import { ConsoleProvider } from "../context/ConsoleContext";
import { SITE_STORAGE_KEY } from "../sites/selection";

export function renderConsole(path: string): RenderResult {
  localStorage.removeItem(SITE_STORAGE_KEY);
  return render(
    <MemoryRouter initialEntries={[path]}>
      <ConsoleProvider>
        <AppRoutes />
      </ConsoleProvider>
    </MemoryRouter>,
  );
}
