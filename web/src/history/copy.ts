// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

export const MISSING_ALERT = "That alert is no longer available. Here are all alerts.";
export const MISSING_TICKET = "That ticket is no longer available. Here are all tickets.";
export const MISSING_INVESTIGATION = "That investigation is no longer available. Here are all investigations.";
export const MISSING_CAPA =
  "That corrective and preventive action is no longer available. Here are all corrective and preventive actions.";

/** The console never shows the API's generic missing-record title. */
export function hiddenMissingTitle(error: string | null | undefined): boolean {
  return error != null && error.length > 0 && /no such record/i.test(error);
}

/** Text safe to put on screen. The generic missing-record title is omitted. */
export function visibleProblem(error: string | null | undefined): string | null {
  if (!error || hiddenMissingTitle(error)) return null;
  return error;
}
