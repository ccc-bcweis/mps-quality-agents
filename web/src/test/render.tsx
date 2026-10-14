// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company
import { render, type RenderResult } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { AppRoutes } from "../App";
import { ConsoleProvider } from "../context/ConsoleContext";

export function renderConsole(path: string): RenderResult {
  return render(
    <MemoryRouter initialEntries={[path]}>
      <ConsoleProvider>
        <AppRoutes />
      </ConsoleProvider>
    </MemoryRouter>,
  );
}
