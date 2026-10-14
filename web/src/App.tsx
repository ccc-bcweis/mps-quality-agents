// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company
import { BrowserRouter, Route, Routes } from "react-router-dom";
import { AppShell } from "./components/AppShell";
import { ConsoleProvider } from "./context/ConsoleContext";
import { FleetPage } from "./pages/FleetPage";
import { NotFoundPage } from "./pages/NotFoundPage";
import { ToolPage } from "./pages/ToolPage";

export function AppRoutes() {
  return (
    <Routes>
      <Route element={<AppShell />}>
        <Route path="/" element={<FleetPage />} />
        <Route path="/tools/:toolId" element={<ToolPage />} />
        <Route path="*" element={<NotFoundPage />} />
      </Route>
    </Routes>
  );
}

export function App() {
  return (
    <BrowserRouter>
      <ConsoleProvider>
        <AppRoutes />
      </ConsoleProvider>
    </BrowserRouter>
  );
}
