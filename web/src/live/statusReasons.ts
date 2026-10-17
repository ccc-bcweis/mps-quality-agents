// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company
import { ruleLabel } from "../labels";
import type { Alert, ManualReading } from "../api/types";

export type StatusReason = { alertId: string; text: string };

/**
 * Plain reasons for a watch or alarm: one line per active alert, naming the rule, or the printout and instrument
 * for a manual reading. Tool status is the worst active alert, so these are exactly the alerts behind it.
 */
export function statusReasons(status: string, alerts: Alert[], manual: ManualReading[]): StatusReason[] {
  if (status === "nominal") return [];
  return alerts
    .filter((alert) => alert.status !== "cleared")
    .map((alert) => {
      const reading = manual.find((row) => row.alertId === alert.id);
      const where = `${alert.parameter}${alert.chamberId ? ` on ${alert.chamberId}` : ""}`;
      const severity = alert.severity === "high" || alert.severity === "critical" ? "Alarm" : "Watch";
      return {
        alertId: alert.id,
        text: reading ? `${severity}: ${reading.source}` : `${severity}: ${ruleLabel(alert.rule)} on ${where}.`,
      };
    });
}
