// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company
import type { AdminSession } from "../api/types";

/** Branded landing page. The app sends signed-out browsers here (ConsoleEntryGate on the server, the shell in the browser). */
const LANDING_PAGE = "/landing.html";

/** After sign-out: the landing page with "You are signed out." */
export const SIGNED_OUT_PAGE = `${LANDING_PAGE}?signedOut=1`;

/** A signed-in account that has no access to this workspace. */
export const NO_ACCESS_PAGE = `${LANDING_PAGE}?noAccess=1`;

/** Entra sign-in that always shows the account picker, then returns to the console. */
export const LOG_IN_URL = "/.auth/login/aad?prompt=select_account&post_login_redirect_uri=/";

/** The landing page that returns to this console route after Log in. */
export function landingFor(path: string): string {
  return path === "/" || path.length === 0 ? LANDING_PAGE : `${LANDING_PAGE}?next=${encodeURIComponent(path)}`;
}

/** Hosted: the platform sign-out clears the session, then lands on the landing page. Local runs have no platform sign-in. */
export function signOutUrl(mode: AdminSession["mode"] | undefined): string {
  return mode === "azure" ? `/.auth/logout?post_logout_redirect_uri=${encodeURIComponent(SIGNED_OUT_PAGE)}` : SIGNED_OUT_PAGE;
}

/**
 * Where the console must send the browser instead of rendering: the landing page when the API says nobody is signed in
 * (401), the no-access landing page when the signed-in account is not part of this workspace. Null keeps the console.
 */
export function entryRedirect(outcome: { session?: AdminSession | null; status?: number }, path: string): string | null {
  if (outcome.status === 401) return landingFor(path);
  if (outcome.status === 403 || outcome.session?.member === false) return NO_ACCESS_PAGE;
  return null;
}
