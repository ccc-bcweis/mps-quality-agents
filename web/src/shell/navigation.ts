// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company
import { S1_INVESTIGATION_ID } from "../mocks/demoData";

export type ConsoleTab = {
  to: string;
  label: string;
  match: (path: string) => boolean;
};

export type HarnessLink = {
  to: string;
  label: string;
  match: (path: string) => boolean;
};

/**
 * Live application tabs only.
 * Lane S2 inserts Sites at index 0, immediately before Fleet.
 * Presenter tools belong in HARNESS_LINKS, not here.
 */
export const CONSOLE_TABS: ConsoleTab[] = [
  { to: "/", label: "Fleet", match: (path) => path === "/" },
  { to: "/tools/E-02", label: "Tool", match: (path) => path.startsWith("/tools") },
  { to: "/alerts", label: "Alerts", match: (path) => path.startsWith("/alerts") },
  { to: "/tickets", label: "Tickets", match: (path) => path.startsWith("/tickets") },
  { to: `/investigations/${S1_INVESTIGATION_ID}`, label: "Investigation", match: (path) => path.startsWith("/investigations") },
  { to: `/approvals/${S1_INVESTIGATION_ID}`, label: "Approval", match: (path) => path.startsWith("/approvals") },
  { to: "/capas", label: "CAPA", match: (path) => path.startsWith("/capas") },
];

export const HARNESS_LINKS: HarnessLink[] = [
  { to: "/harness/injector", label: "Fault injector", match: (path) => path.startsWith("/harness/injector") },
  { to: "/harness/roi", label: "Return on investment", match: (path) => path.startsWith("/harness/roi") },
  { to: "/harness/metrics", label: "Metrics", match: (path) => path.startsWith("/harness/metrics") },
];
