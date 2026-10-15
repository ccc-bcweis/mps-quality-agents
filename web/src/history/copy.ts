// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

export const MISSING_ALERT = "That alert is no longer available. Here are all alerts.";
export const MISSING_TICKET = "That ticket is no longer available. Here are all tickets.";
export const MISSING_INVESTIGATION = "That investigation is no longer available. Here are all investigations.";
export const MISSING_CAPA =
  "That corrective and preventive action is no longer available. Here are all corrective and preventive actions.";

/** The console never shows the API's generic missing-record title. */
export function hiddenMissingTitle(error: string | null): boolean {
  return error !== null && /no such record/i.test(error);
}
