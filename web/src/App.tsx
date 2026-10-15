// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company
import { BrowserRouter, Navigate, Route, Routes } from "react-router-dom";
import { AppShell, ConsoleFrame, HarnessFrame } from "./components/AppShell";
import { ConsoleProvider } from "./context/ConsoleContext";
import { AlertsPage } from "./pages/AlertsPage";
import { ApprovalPage } from "./pages/ApprovalPage";
import { CapaPage } from "./pages/CapaPage";
import { FleetPage } from "./pages/FleetPage";
import { GlossaryPage } from "./pages/GlossaryPage";
import { InjectorPage } from "./pages/InjectorPage";
import { InvestigationPage } from "./pages/InvestigationPage";
import { MetricsPage } from "./pages/MetricsPage";
import { NotFoundPage } from "./pages/NotFoundPage";
import { RoiPage } from "./pages/RoiPage";
import { TicketsPage } from "./pages/TicketsPage";
import { ToolPage } from "./pages/ToolPage";

export function AppRoutes() {
  return (
    <Routes>
      <Route element={<AppShell />}>
        <Route element={<HarnessFrame />}>
          <Route path="/harness/injector" element={<InjectorPage />} />
          <Route path="/harness/roi" element={<RoiPage />} />
          <Route path="/harness/metrics" element={<MetricsPage />} />
        </Route>
        <Route path="/injector" element={<Navigate to="/harness/injector" replace />} />
        <Route path="/roi" element={<Navigate to="/harness/roi" replace />} />
        <Route path="/metrics" element={<Navigate to="/harness/metrics" replace />} />
        <Route element={<ConsoleFrame />}>
          <Route path="/" element={<FleetPage />} />
          <Route path="/tools/:toolId" element={<ToolPage />} />
          <Route path="/alerts" element={<AlertsPage />} />
          <Route path="/tickets" element={<TicketsPage />} />
          <Route path="/investigations/:investigationId" element={<InvestigationPage />} />
          <Route path="/approvals/:investigationId" element={<ApprovalPage />} />
          <Route path="/capas" element={<CapaPage />} />
          <Route path="/capas/:capaId" element={<CapaPage />} />
          <Route path="/glossary" element={<GlossaryPage />} />
          <Route path="*" element={<NotFoundPage />} />
        </Route>
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
